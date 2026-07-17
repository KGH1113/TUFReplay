import AVFoundation
import AppKit
import CoreMedia
import Foundation

final class MicrophoneCaptureService: NSObject, AVCaptureAudioDataOutputSampleBufferDelegate {
  private let lock = NSLock()
  private let callbackQueue = DispatchQueue(label: "impl.tufreplay.microphone.callback")
  private var session: AVCaptureSession?
  private var output: AVCaptureAudioDataOutput?
  private var writer: PcmWaveFileWriter?
  private var beginTime: UInt64 = 0
  private var firstBufferOffsetUs: Int64 = 0
  private var activeDeviceId: String?

  func devices() -> [MicrophoneDeviceResponse] {
    discoverySession().devices.map {
      MicrophoneDeviceResponse(id: $0.uniqueID, name: $0.localizedName)
    }
  }

  func authorize() throws {
    try ensurePermission()
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
    guard session?.isRunning == true else { throw CaptureError.message("Microphone is not armed.") }
    lock.lock()
    defer { lock.unlock() }
    try finishWriterLocked()
    writer = try PcmWaveFileWriter(path: path)
    firstBufferOffsetUs = 0
    beginTime = DispatchTime.now().uptimeNanoseconds
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
    guard let block = CMSampleBufferGetDataBuffer(sampleBuffer) else { return }
    let length = CMBlockBufferGetDataLength(block)
    guard length > 0 else { return }
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
    guard let writer else { return }
    if writer.frameCount == 0 {
      let elapsed = DispatchTime.now().uptimeNanoseconds - beginTime
      firstBufferOffsetUs = Int64(elapsed / 1_000)
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
    switch AVCaptureDevice.authorizationStatus(for: .audio) {
    case .authorized:
      return
    case .notDetermined:
      let semaphore = DispatchSemaphore(value: 0)
      var granted = false
      DispatchQueue.main.async {
        NSApplication.shared.activate(ignoringOtherApps: true)
        AVCaptureDevice.requestAccess(for: .audio) { value in
          granted = value
          semaphore.signal()
          DispatchQueue.main.async {
            NSApplication.shared.hide(nil)
          }
        }
      }
      guard semaphore.wait(timeout: .now() + 120) == .success else {
        throw CaptureError.message("Microphone permission request timed out.")
      }
      if !granted {
        throw CaptureError.message("Microphone permission request was denied by the user.")
      }
    case .denied:
      throw CaptureError.message("Microphone authorization status is denied.")
    case .restricted:
      throw CaptureError.message("Microphone authorization status is restricted.")
    @unknown default:
      throw CaptureError.message("Microphone authorization status is unknown.")
    }
  }

  @discardableResult
  private func finishWriterLocked() throws -> Int64 {
    guard let writer else { return 0 }
    let frameCount = try writer.finish()
    self.writer = nil
    return frameCount
  }
}
