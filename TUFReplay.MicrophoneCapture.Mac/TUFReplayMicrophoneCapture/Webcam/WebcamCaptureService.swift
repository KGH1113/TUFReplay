import AppKit
import AVFoundation
import CoreMedia
import CoreVideo

struct WebcamProfile: Equatable {
  let width: Int
  let height: Int
  let frameRate: Int
  let bitRate: Int

  func fitting(width: Int, height: Int) -> WebcamProfile {
    let size = CameraFrameSize.fit(width: width, height: height, maxWidth: self.width, maxHeight: self.height)
    return WebcamProfile(width: size.width, height: size.height, frameRate: frameRate, bitRate: bitRate)
  }

  func validate() throws {
    guard width > 0, width <= 1920, height > 0, height <= 1080,
      width % 2 == 0, height % 2 == 0, frameRate > 0, frameRate <= 60,
      bitRate >= 100_000, bitRate <= 8_000_000 else {
      throw CaptureError.message("Unsupported camera quality. Choose Compact, Balanced, or Quality.")
    }
  }

  var outputSettings: [String: Any] {
    [
      AVVideoCodecKey: AVVideoCodecType.h264,
      AVVideoWidthKey: width,
      AVVideoHeightKey: height,
      AVVideoScalingModeKey: AVVideoScalingModeResizeAspect,
      AVVideoCompressionPropertiesKey: [
        AVVideoAverageBitRateKey: bitRate,
        AVVideoExpectedSourceFrameRateKey: frameRate,
        AVVideoMaxKeyFrameIntervalKey: frameRate * 2,
        AVVideoProfileLevelKey: AVVideoProfileLevelH264MainAutoLevel,
        AVVideoAllowFrameReorderingKey: false,
      ],
    ]
  }
}

// Prepare a single empty destination while capture keeps running. No camera
// samples are appended until a run claims this writer.
final class CameraPreparedWriter {
  let writer: AVAssetWriter
  let input: AVAssetWriterInput
  let profile: WebcamProfile

  init(directory: String, profile: WebcamProfile) throws {
    self.profile = profile
    try FileManager.default.createDirectory(atPath: directory, withIntermediateDirectories: true)
    let url = URL(fileURLWithPath: directory).appendingPathComponent(UUID().uuidString + ".mp4.partial")
    writer = try AVAssetWriter(outputURL: url, fileType: .mp4)
    input = AVAssetWriterInput(mediaType: .video, outputSettings: profile.outputSettings)
    input.expectsMediaDataInRealTime = true
    guard writer.canAdd(input) else {
      cancel()
      throw CaptureError.message("H.264 camera encoding is unavailable.")
    }
    writer.add(input)
    writer.shouldOptimizeForNetworkUse = true
    guard writer.startWriting() else {
      let error = writer.error ?? CaptureError.message("Video recording could not start.")
      cancel()
      throw error
    }
  }

  func finish(destination: String) throws {
    let done = DispatchSemaphore(value: 0)
    writer.finishWriting { done.signal() }
    guard done.wait(timeout: .now() + 20) == .success, writer.status == .completed else {
      let error = writer.error ?? CaptureError.message("The camera video could not be saved. Try reconnecting the camera.")
      cancel()
      throw error
    }
    do {
      try FileManager.default.moveItem(at: writer.outputURL, to: URL(fileURLWithPath: destination))
    } catch {
      cancel()
      throw error
    }
  }

  func cancel() {
    if writer.status == .writing || writer.status == .unknown { writer.cancelWriting() }
    try? FileManager.default.removeItem(at: writer.outputURL)
  }
}

final class WebcamCaptureService: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate {
  private let callbackQueue = DispatchQueue(label: "impl.tufreplay.webcam.frames", qos: .userInitiated)
  private let preparationQueue = DispatchQueue(label: "impl.tufreplay.webcam.prepare", qos: .userInitiated)
  private var session: AVCaptureSession?
  private var output: AVCaptureVideoDataOutput?
  private var profile = WebcamProfile(width: 640, height: 480, frameRate: 30, bitRate: 600_000)
  private var requestedProfile = WebcamProfile(width: 640, height: 480, frameRate: 30, bitRate: 600_000)
  private var deviceId: String?
  // Only callbackQueue accesses the writer and its timing state.
  private var activeWriter: CameraPreparedWriter?
  private var standbyWriter: CameraPreparedWriter?
  private var preparingWriter = false
  private var preparationGeneration = 0
  private var preparationError: Error?
  private var recordingDirectory: String?
  private var destination: String?
  private var latestSample: CMSampleBuffer?
  private var latestHostTime: CMTime = .invalid
  private var writer: AVAssetWriter? { activeWriter?.writer }
  private var input: AVAssetWriterInput? { activeWriter?.input }
  private var boundaryHostTime: CMTime = .invalid
  private var firstTime: CMTime = .invalid
  private var lastTime: CMTime = .invalid
  private var nextFrameTime: CMTime = .invalid
  private var firstFrameHostTime: UInt64 = 0
  private var maxDuration: Double = 0
  private var maxFileBytes: Int64 = 0
  private var lastSizeCheck: CMTime = .invalid
  private var sizeLimited = false
  private var captureError: Error?
  private var preview: CameraPreviewWorker?

  func devices() -> [CameraDeviceResponse] {
    let devices = availableDevices()

    return devices.map { CameraDeviceResponse(id: $0.uniqueID, name: $0.localizedName) }
  }

  private func availableDevices() -> [AVCaptureDevice] {
    let types: [AVCaptureDevice.DeviceType]
    if #available(macOS 14.0, *) {
      types = [.builtInWideAngleCamera, .external, .continuityCamera]
    } else {
      types = [.builtInWideAngleCamera, .externalUnknown]
    }
    return AVCaptureDevice.DiscoverySession(deviceTypes: types, mediaType: .video, position: .unspecified).devices
  }

  func arm(deviceId: String?, profile: WebcamProfile, previewPath: String? = nil, recordingDirectory: String? = nil) throws {
    disarm()
    callbackQueue.sync {
      captureError = nil
      self.recordingDirectory = recordingDirectory ?? FileManager.default.temporaryDirectory.path
    }

    try profile.validate()
    try ensurePermission()
    let discovered = availableDevices()

    let device = deviceId == nil ? AVCaptureDevice.default(for: .video)
      : discovered.first { $0.uniqueID == deviceId }
    guard let device else {
      throw CaptureError.message("The selected camera is unavailable. Reconnect it or select another camera.")
    }

    let capture = AVCaptureSession()
    capture.beginConfiguration()
    var configuring = true
    defer { if configuring { capture.commitConfiguration() } }
    // A resolution preset can crop a widescreen camera to 4:3. Choose a device
    // format with its native ratio, then fit the encoder output within the budget.
    let nativeSize = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
    let source = try AVCaptureDeviceInput(device: device)
    guard capture.canAddInput(source) else {
      throw CaptureError.message("The camera could not be opened. Close other apps using it and try again.")
    }
    capture.addInput(source)
    let frames = AVCaptureVideoDataOutput()
    frames.alwaysDiscardsLateVideoFrames = true
    frames.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange]
    frames.setSampleBufferDelegate(self, queue: callbackQueue)
    guard capture.canAddOutput(frames) else {
      throw CaptureError.message("Camera frames are unavailable. Reconnect the camera and try again.")
    }
    capture.addOutput(frames)
    try device.lockForConfiguration()
    let target = CameraFrameSize.fit(width: Int(nativeSize.width), height: Int(nativeSize.height),
      maxWidth: max(profile.width, CameraPreviewBuffer.maxWidth),
      maxHeight: max(profile.height, CameraPreviewBuffer.maxHeight))
    let nativeAspect = Double(nativeSize.width) / Double(nativeSize.height)
    func nativeAspectFormats(supporting frameRate: Int) -> [AVCaptureDevice.Format] {
      device.formats.filter { format in
        let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
        return size.width > 0 && size.height > 0
          && abs(Double(size.width) / Double(size.height) - nativeAspect) < 0.01
          && format.videoSupportedFrameRateRanges.contains {
            $0.minFrameRate <= Double(frameRate) && $0.maxFrameRate >= Double(frameRate)
          }
      }
    }
    // Quality asks for 60 fps; most webcams only offer 30. Fall back explicitly so
    // the encoder's expected rate and key-frame interval match the capture.
    var frameRate = profile.frameRate
    var formats = nativeAspectFormats(supporting: frameRate)
    if formats.isEmpty && frameRate > 30 {
      frameRate = 30
      formats = nativeAspectFormats(supporting: frameRate)
    }
    let captureProfile = WebcamProfile(
      width: profile.width, height: profile.height, frameRate: frameRate, bitRate: profile.bitRate)
    let suitable = formats.filter { format in
      let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
      return size.width >= target.width && size.height >= target.height
    }
    let selected = suitable.min { first, second in
      let a = CMVideoFormatDescriptionGetDimensions(first.formatDescription)
      let b = CMVideoFormatDescriptionGetDimensions(second.formatDescription)
      return Int64(a.width) * Int64(a.height) < Int64(b.width) * Int64(b.height)
    } ?? formats.max { first, second in
      let a = CMVideoFormatDescriptionGetDimensions(first.formatDescription)
      let b = CMVideoFormatDescriptionGetDimensions(second.formatDescription)
      return Int64(a.width) * Int64(a.height) < Int64(b.width) * Int64(b.height)
    }
    if let selected { device.activeFormat = selected }
    if device.activeFormat.videoSupportedFrameRateRanges.contains(where: {
      $0.minFrameRate <= Double(captureProfile.frameRate) && $0.maxFrameRate >= Double(captureProfile.frameRate)
    }) {
      let duration = CMTime(value: 1, timescale: CMTimeScale(captureProfile.frameRate))
      device.activeVideoMinFrameDuration = duration
      device.activeVideoMaxFrameDuration = duration
    }
    device.unlockForConfiguration()

    let sourceSize = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
    // Frame-size refits start from what the camera can deliver, keeping the fallback.
    requestedProfile = captureProfile
    self.profile = captureProfile.fitting(width: Int(sourceSize.width), height: Int(sourceSize.height))
    self.deviceId = device.uniqueID
    if let previewPath {
      let buffer = try CameraPreviewBuffer(path: previewPath,
        width: CameraPreviewBuffer.maxWidth, height: CameraPreviewBuffer.maxHeight)
      preview = CameraPreviewWorker { pixels, time in buffer.publish(pixels, time: time) }
    }
    session = capture
    output = frames
    capture.commitConfiguration()
    configuring = false

    capture.startRunning()

    callbackQueue.sync { prepareNextWriter() }
    preparationQueue.sync {}
    callbackQueue.sync {}
  }

  private func invalidateStandby() {
    preparationGeneration += 1
    preparingWriter = false
    preparationError = nil
    standbyWriter?.cancel()
    standbyWriter = nil
  }

  private func prepareNextWriter() {
    guard activeWriter == nil, standbyWriter == nil, !preparingWriter, preparationError == nil,
      let directory = recordingDirectory, session?.isRunning == true else { return }
    let generation = preparationGeneration
    let preparedProfile = profile
    preparingWriter = true
    preparationQueue.async {
      do {
        let prepared = try CameraPreparedWriter(directory: directory, profile: preparedProfile)
        self.callbackQueue.async {
          guard self.preparationGeneration == generation, self.recordingDirectory != nil,
            self.activeWriter == nil, self.profile == preparedProfile else {
            prepared.cancel()
            return
          }
          self.standbyWriter = prepared
          self.preparingWriter = false

        }
      } catch {
        self.callbackQueue.async {
          guard self.preparationGeneration == generation else { return }
          self.preparingWriter = false
          self.preparationError = error

        }
      }
    }
  }

  func begin(path: String, maxBytes: Int64, startHostTime: UInt64? = nil) throws {
    let requestedBoundary = startHostTime.map { CMClockMakeHostTimeFromSystemUnits($0) }
      ?? CMClockGetTime(CMClockGetHostTimeClock())

    guard session?.isRunning == true else {
      throw CaptureError.message("The camera is warming up. Wait a moment before starting the next run.")
    }
    guard maxBytes >= 2 * 1024 * 1024 else {
      throw CaptureError.message("Camera storage is full. Increase the storage limit or remove older videos.")
    }
    let initial = callbackQueue.sync { (latestSample, latestHostTime, preparingWriter) }
    if initial.2 {
      // Wait on the command thread, while camera callbacks continue normally.
      preparationQueue.sync {}
      callbackQueue.sync {}
    }
    let slot = try callbackQueue.sync { () -> (CameraPreparedWriter?, WebcamProfile) in
      guard activeWriter == nil else { throw CaptureError.message("A camera recording is already running.") }
      let prepared = standbyWriter
      standbyWriter = nil
      preparationGeneration += 1
      preparingWriter = false
      return (prepared, profile)
    }
    let prepared = try slot.0 ?? CameraPreparedWriter(directory: URL(fileURLWithPath: path).deletingLastPathComponent().path,
      profile: slot.1)
    callbackQueue.sync {
      activeWriter = prepared
      preparationError = nil
      profile = prepared.profile
      destination = path
      boundaryHostTime = requestedBoundary
      firstTime = .invalid
      lastTime = .invalid
      nextFrameTime = .invalid
      firstFrameHostTime = 0
      captureError = nil
      sizeLimited = false
      maxFileBytes = maxBytes - 1024 * 1024
      lastSizeCheck = .invalid
      // Leave room for the MP4 index and bitrate variability. The store also
      // enforces the actual byte limit before retaining a finished recording.
      maxDuration = Double(maxBytes - 1024 * 1024) * 8 / (Double(profile.bitRate) * 1.5)

      // A delivered frame can be tens of milliseconds older than the wall
      // clock. Start from the latest complete sample instead of throwing it
      // away and waiting for camera latency again. Preserve its original PTS.
      if let sample = initial.0, initial.1.isValid {
        if Self.canReuseLatestSample(hostTime: initial.1, boundary: CMClockGetTime(CMClockGetHostTimeClock()),
          minimumHostTime: startHostTime == nil ? .invalid : requestedBoundary) {
          boundaryHostTime = initial.1
          append(sample, pts: CMSampleBufferGetPresentationTimeStamp(sample), hostTime: initial.1)
        }
      }
    }
  }

  static func canReuseLatestSample(hostTime: CMTime, boundary: CMTime, minimumHostTime: CMTime = .invalid) -> Bool {
    guard hostTime.isValid, !hostTime.isIndefinite, boundary.isValid, !boundary.isIndefinite else { return false }
    let age = CMTimeGetSeconds(CMTimeSubtract(boundary, hostTime))
    return age >= 0 && age <= 0.25 && (!minimumHostTime.isValid || CMTimeCompare(hostTime, minimumHostTime) >= 0)
  }

  func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
    let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
    if let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) {
      if writer == nil {
        let fitted = requestedProfile.fitting(width: CVPixelBufferGetWidth(pixels), height: CVPixelBufferGetHeight(pixels))
        if fitted != profile {
          profile = fitted
          invalidateStandby()
        }
        prepareNextWriter()
      }
      preview?.submit(pixels, time: ProcessInfo.processInfo.systemUptime)
    }
    if let clock = session?.masterClock, pts.isValid, !pts.isIndefinite {
      let host = CMSyncConvertTime(pts, from: clock, to: CMClockGetHostTimeClock())
      if host.isValid, !host.isIndefinite {
        if CMSampleBufferGetImageBuffer(sampleBuffer) != nil {
          latestSample = sampleBuffer
          latestHostTime = host
        }
      }
    }
    guard writer != nil else { return }
    guard let clock = session?.masterClock, pts.isValid, !pts.isIndefinite else {

      return
    }
    append(sampleBuffer, pts: pts, hostTime: CMSyncConvertTime(pts, from: clock, to: CMClockGetHostTimeClock()))
  }

  private func append(_ sampleBuffer: CMSampleBuffer, pts: CMTime, hostTime: CMTime) {
    guard let writer, let input else { return }
    guard !sizeLimited else { return }
    guard captureError == nil else { return }
    guard input.isReadyForMoreMediaData else {

      return
    }
    guard pts.isValid, !pts.isIndefinite else {

      return
    }
    guard hostTime.isValid, !hostTime.isIndefinite else {

      return
    }
    guard CMTimeCompare(hostTime, boundaryHostTime) >= 0 else { return }
    if firstTime.isValid {
      guard CMTimeCompare(pts, lastTime) > 0 else { return }
      // Some cameras only deliver 30 fps even when 24 fps was requested.
      // Drop excess frames without copying pixels or changing their timestamps.
      guard !nextFrameTime.isValid || CMTimeCompare(pts, nextFrameTime) >= 0 else { return }
      if CMTimeGetSeconds(CMTimeSubtract(pts, firstTime)) >= maxDuration {
        sizeLimited = true

        return
      }
      if !lastSizeCheck.isValid || CMTimeGetSeconds(CMTimeSubtract(pts, lastSizeCheck)) >= 1 {
        lastSizeCheck = pts
        let bytes = (try? FileManager.default.attributesOfItem(atPath: writer.outputURL.path)[.size] as? NSNumber)?.int64Value ?? 0
        if bytes >= maxFileBytes {
          sizeLimited = true

          return
        }
      }
    } else {
      writer.startSession(atSourceTime: pts)
    }
    guard input.append(sampleBuffer) else {
      captureError = writer.error ?? CaptureError.message("Camera frames could not be encoded.")

      return
    }
    if !firstTime.isValid {
      firstTime = pts
      firstFrameHostTime = CMClockConvertHostTimeToSystemUnits(hostTime)

    }
    lastTime = pts
    let nextIndex = CMTimeConvertScale(CMTimeSubtract(pts, firstTime), timescale: CMTimeScale(profile.frameRate), method: .roundTowardZero).value + 1
    nextFrameTime = CMTimeAdd(firstTime, CMTime(value: nextIndex, timescale: CMTimeScale(profile.frameRate)))
  }

  func end() throws -> CameraEndResponse {

    let state = callbackQueue.sync { () -> (CameraPreparedWriter?, Error?, UInt64, Int64, Bool, Int, Int, String?) in
      let prepared = activeWriter
      if firstTime.isValid { input?.markAsFinished() }
      let duration = firstTime.isValid
        ? max(1, Int64((CMTimeGetSeconds(CMTimeSubtract(lastTime, firstTime)) + 1 / Double(profile.frameRate)) * 1_000_000)) : 0
      let result = (prepared, captureError, firstFrameHostTime, duration, sizeLimited, profile.width, profile.height, destination)
      activeWriter = nil
      destination = nil
      prepareNextWriter()
      return result
    }
    if let error = state.1 { state.0?.cancel(); throw error }
    if let prepared = state.0 {
      if state.2 == 0 { prepared.cancel() }
      else if let path = state.7 { try prepared.finish(destination: path) }
    }

    return CameraEndResponse(deviceId: deviceId, firstFrameHostTime: state.2, durationUs: state.3, sizeLimited: state.4,
      width: state.5, height: state.6)
  }

  func disarm() {
    callbackQueue.sync {
      recordingDirectory = nil
      invalidateStandby()
      activeWriter?.cancel()
      activeWriter = nil
      destination = nil
      latestSample = nil
      latestHostTime = .invalid
      preview?.stop()
      preview = nil
    }
    session?.stopRunning()
    // Drain a preparation already in progress and its cancellation callback.
    preparationQueue.sync {}
    callbackQueue.sync {}
    output?.setSampleBufferDelegate(nil, queue: nil)
    output = nil
    session = nil
    deviceId = nil

  }

  private func ensurePermission() throws {

    if AVCaptureDevice.authorizationStatus(for: .video) == .notDetermined {
      let completed = DispatchSemaphore(value: 0)
      DispatchQueue.main.async {

        NSApplication.shared.activate(ignoringOtherApps: true)
        AVCaptureDevice.requestAccess(for: .video) { granted in

          completed.signal()
          DispatchQueue.main.async { NSApplication.shared.hide(nil) }
        }
      }
      guard completed.wait(timeout: .now() + 120) == .success else {

        throw CaptureError.message("Camera permission is still pending. Allow camera access and try again.")
      }
    }
    guard AVCaptureDevice.authorizationStatus(for: .video) == .authorized else {
      throw CaptureError.message("Camera access is off. Allow TUFReplay Microphone Capture in System Settings > Privacy & Security > Camera.")
    }

  }
}
