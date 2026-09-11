import AVFoundation
import AppKit
import CoreMedia
import Foundation

struct CaptureBufferSlice {
  let skippedFrames: Int
  let startOffsetUs: Int64
}

enum CaptureBufferTiming {
  static func firstWritableSlice(
    presentationTime: CMTime,
    beginTime: CMTime,
    sampleRate: Int,
    sampleCount: Int
  ) -> CaptureBufferSlice? {
    guard
      presentationTime.isValid,
      !presentationTime.isIndefinite,
      beginTime.isValid,
      sampleRate > 0,
      sampleCount > 0
    else { return nil }

    let firstSampleSeconds = CMTimeGetSeconds(CMTimeSubtract(presentationTime, beginTime))
    guard firstSampleSeconds.isFinite else { return nil }
    let skippedFrames = min(
      sampleCount,
      max(0, Int(ceil(-firstSampleSeconds * Double(sampleRate))))
    )
    guard skippedFrames < sampleCount else { return nil }

    let firstWrittenTime = CMTimeAdd(
      presentationTime,
      CMTime(value: Int64(skippedFrames), timescale: CMTimeScale(sampleRate))
    )
    let offsetSeconds = CMTimeGetSeconds(CMTimeSubtract(firstWrittenTime, beginTime))
    guard offsetSeconds.isFinite else { return nil }
    return CaptureBufferSlice(
      skippedFrames: skippedFrames,
      startOffsetUs: max(0, Int64((offsetSeconds * 1_000_000).rounded()))
    )
  }
}

final class MicrophoneCaptureService: NSObject, AVCaptureAudioDataOutputSampleBufferDelegate {
  private let lock = NSLock()
  private let callbackQueue = DispatchQueue(label: "impl.tufreplay.microphone.callback")
  private var session: AVCaptureSession?
  private var output: AVCaptureAudioDataOutput?
  private var writer: PcmWaveFileWriter?
  private var beginTime: CMTime = .invalid
  private var firstBufferOffsetUs: Int64 = 0
  private var activeDeviceId: String?

  func devices() -> [MicrophoneDeviceResponse] {
    discoverySession().devices.map {
      MicrophoneDeviceResponse(id: $0.uniqueID, name: $0.localizedName)
    }
  }

  func authorizationStatus() throws -> MicrophoneAuthorizationStatus {
    switch AVCaptureDevice.authorizationStatus(for: .audio) {
    case .notDetermined:
      return .notDetermined
    case .authorized:
      return .authorized
    case .denied:
      return .denied
    case .restricted:
      return .restricted
    @unknown default:
      throw CaptureError.message("Microphone authorization status is unknown.")
    }
  }

  func authorize() throws -> MicrophoneAuthorizationStatus {
    let currentStatus = try authorizationStatus()
    guard currentStatus == .notDetermined else { return currentStatus }

    let semaphore = DispatchSemaphore(value: 0)
    DispatchQueue.main.async {
      NSApplication.shared.activate(ignoringOtherApps: true)
      AVCaptureDevice.requestAccess(for: .audio) { _ in
        semaphore.signal()
        DispatchQueue.main.async {
          NSApplication.shared.hide(nil)
        }
      }
    }
    guard semaphore.wait(timeout: .now() + 120) == .success else {
      throw CaptureError.message("Microphone permission request timed out.")
    }
    return try authorizationStatus()
  }

  func arm(deviceId: String?) throws {
    disarm()
    try ensurePermission()
    let devices = discoverySession().devices
    let device = try resolveDevice(deviceId: deviceId, devices: devices)

    let newSession = AVCaptureSession()
    let input = try AVCaptureDeviceInput(device: device)
    guard newSession.canAddInput(input) else {
      throw CaptureError.message("Cannot attach microphone input.")
    }
    newSession.addInput(input)

    let newOutput = AVCaptureAudioDataOutput()
    newOutput.audioSettings = [
      AVFormatIDKey: kAudioFormatLinearPCM,
      AVSampleRateKey: PcmWaveFile.sampleRate,
      AVNumberOfChannelsKey: PcmWaveFile.channels,
      AVLinearPCMBitDepthKey: PcmWaveFile.bitsPerSample,
      AVLinearPCMIsFloatKey: false,
      AVLinearPCMIsBigEndianKey: false,
      AVLinearPCMIsNonInterleaved: false,
    ]
    newOutput.setSampleBufferDelegate(self, queue: callbackQueue)
    guard newSession.canAddOutput(newOutput) else {
      throw CaptureError.message("Cannot attach microphone output.")
    }
    newSession.addOutput(newOutput)

    session = newSession
    output = newOutput
    activeDeviceId = device.uniqueID
    newSession.startRunning()
  }

  func begin(path: String) throws {
    guard let session, session.isRunning, let masterClock = session.masterClock else {
      throw CaptureError.message("Microphone is not armed.")
    }
    lock.lock()
    defer { lock.unlock() }
    try finishWriterLocked()
    writer = try PcmWaveFileWriter(path: path)
    firstBufferOffsetUs = 0
    beginTime = CMClockGetTime(masterClock)
  }

  func end() throws -> CaptureEndResponse {
    lock.lock()
    defer { lock.unlock() }
    let frameCount = try finishWriterLocked()
    return CaptureEndResponse(
      frameCount: frameCount,
      deviceId: activeDeviceId,
      captureStartOffsetUs: firstBufferOffsetUs
    )
  }

  func disarm() {
    lock.lock()
    _ = try? finishWriterLocked()
    lock.unlock()
    session?.stopRunning()
    output?.setSampleBufferDelegate(nil, queue: nil)
    output = nil
    session = nil
    activeDeviceId = nil
  }

  func captureOutput(
    _ output: AVCaptureOutput,
    didOutput sampleBuffer: CMSampleBuffer,
    from connection: AVCaptureConnection
  ) {
    let sampleCount = CMSampleBufferGetNumSamples(sampleBuffer)
    let presentationTime = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
    guard sampleCount > 0, presentationTime.isValid, !presentationTime.isIndefinite else { return }
    guard let block = CMSampleBufferGetDataBuffer(sampleBuffer) else { return }
    let length = CMBlockBufferGetDataLength(block)
    guard length > 0, length % sampleCount == 0 else { return }
    var data = Data(count: length)
    let copyStatus = data.withUnsafeMutableBytes { buffer in
      CMBlockBufferCopyDataBytes(
        block,
        atOffset: 0,
        dataLength: length,
        destination: buffer.baseAddress!
      )
    }
    guard copyStatus == kCMBlockBufferNoErr else { return }

    lock.lock()
    defer { lock.unlock() }
    guard let writer, beginTime.isValid else { return }
    if writer.frameCount == 0 {
      guard
        let slice = CaptureBufferTiming.firstWritableSlice(
          presentationTime: presentationTime,
          beginTime: beginTime,
          sampleRate: PcmWaveFile.sampleRate,
          sampleCount: sampleCount
        )
      else { return }

      let bytesPerFrame = length / sampleCount
      if slice.skippedFrames > 0 {
        data = Data(data.dropFirst(slice.skippedFrames * bytesPerFrame))
      }
      firstBufferOffsetUs = slice.startOffsetUs
    }
    do {
      try writer.append(pcm16: data)
    } catch {
      fputs("audio write failed: \(error)\n", stderr)
    }
  }

  private func discoverySession() -> AVCaptureDevice.DiscoverySession {
    AVCaptureDevice.DiscoverySession(
      deviceTypes: [.builtInMicrophone, .externalUnknown],
      mediaType: .audio,
      position: .unspecified
    )
  }

  private func resolveDevice(
    deviceId: String?,
    devices: [AVCaptureDevice]
  ) throws -> AVCaptureDevice {
    guard let deviceId else {
      guard let device = AVCaptureDevice.default(for: .audio) else {
        throw CaptureError.message("No microphone is available.")
      }
      return device
    }

    if let device = devices.first(where: { $0.uniqueID == deviceId }) {
      return device
    }

    let legacyNameMatches = devices.filter { $0.localizedName == deviceId }
    guard legacyNameMatches.count == 1, let device = legacyNameMatches.first else {
      throw CaptureError.message("The selected microphone is unavailable.")
    }
    return device
  }

  private func ensurePermission() throws {
    switch try authorize() {
    case .authorized:
      return
    case .denied:
      throw CaptureError.message("Microphone authorization status is denied.")
    case .restricted:
      throw CaptureError.message("Microphone authorization status is restricted.")
    case .notDetermined:
      throw CaptureError.message("Microphone authorization status is still undetermined.")
    }
  }

  @discardableResult
  private func finishWriterLocked() throws -> Int64 {
    guard let writer else { return 0 }
    let frameCount = try writer.finish()
    self.writer = nil
    beginTime = .invalid
    return frameCount
  }
}
