import AppKit
import AVFoundation
import CoreMedia
import CoreVideo

struct WebcamProfile {
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
      width % 2 == 0, height % 2 == 0, frameRate > 0, frameRate <= 30,
      bitRate >= 100_000, bitRate <= 3_000_000 else {
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

final class WebcamCaptureService: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate {
  private let callbackQueue = DispatchQueue(label: "impl.tufreplay.webcam.frames", qos: .userInitiated)
  private var session: AVCaptureSession?
  private var output: AVCaptureVideoDataOutput?
  private var profile = WebcamProfile(width: 640, height: 480, frameRate: 30, bitRate: 600_000)
  private var requestedProfile = WebcamProfile(width: 640, height: 480, frameRate: 30, bitRate: 600_000)
  private var deviceId: String?
  // Only callbackQueue accesses the writer and its timing state.
  private var writer: AVAssetWriter?
  private var input: AVAssetWriterInput?
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
  private var previewBuffer: CameraPreviewBuffer?
  private let diagnostics = CameraDiagnostics.shared
  private var selectedDevice: AVCaptureDevice?
  private var observations: [NSObjectProtocol] = []
  private var healthTimer: DispatchSourceTimer?
  private var statistics = CameraCaptureStatistics()
  private var phase = "idle"
  private var captureId: String?
  private var runId: String?
  private var lastRuntimeError: Error?
  private var lastInterruption: [String: Any]?
  private var sessionInterrupted = false
  private var receivedAtBegin: Int64 = 0
  private var encodedAtBegin: Int64 = 0
  private var sourceWidth = 0
  private var sourceHeight = 0
  private var sourcePixelFormat: OSType = 0

  func devices() -> [CameraDeviceResponse] {
    let devices = availableDevices()
    diagnostics.record("devices.discovered", [
      "authorization": authorizationName(), "devices": devices.map { deviceDetails($0) },
      "defaultDeviceId": AVCaptureDevice.default(for: .video)?.uniqueID as Any? ?? NSNull(),
    ])
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

  func arm(deviceId: String?, profile: WebcamProfile, previewPath: String? = nil) throws {
    disarm()
    captureId = UUID().uuidString
    callbackQueue.sync {
      statistics = CameraCaptureStatistics()
      sourceWidth = 0
      sourceHeight = 0
      sourcePixelFormat = 0
      lastRuntimeError = nil
      lastInterruption = nil
      sessionInterrupted = false
      captureError = nil
      runId = nil
      receivedAtBegin = 0
      encodedAtBegin = 0
    }
    phase = "profile.validate"
    diagnostics.record("capture.arm.begin", [
      "captureId": captureId as Any? ?? NSNull(), "requestedDeviceId": deviceId as Any? ?? NSNull(),
      "width": profile.width, "height": profile.height, "frameRate": profile.frameRate, "bitRate": profile.bitRate,
    ])
    try profile.validate()
    phase = "permission"
    try ensurePermission()
    phase = "device.select"
    let discovered = availableDevices()
    diagnostics.record("device.selection", [
      "requestedDeviceId": deviceId as Any? ?? NSNull(), "available": discovered.map { deviceDetails($0) },
      "defaultDeviceId": AVCaptureDevice.default(for: .video)?.uniqueID as Any? ?? NSNull(),
    ])
    let device = deviceId == nil ? AVCaptureDevice.default(for: .video)
      : discovered.first { $0.uniqueID == deviceId }
    guard let device else {
      throw CaptureError.message("The selected camera is unavailable. Reconnect it or select another camera.")
    }
    selectedDevice = device
    diagnostics.record("device.selected", deviceDetails(device))
    let capture = AVCaptureSession()
    capture.beginConfiguration()
    var configuring = true
    defer { if configuring { capture.commitConfiguration() } }
    // A resolution preset can crop a widescreen camera to 4:3. Choose a device
    // format with its native ratio, then fit the encoder output within the budget.
    let nativeSize = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
    phase = "device.input.create"
    let source = try AVCaptureDeviceInput(device: device)
    phase = "session.input.add"
    guard capture.canAddInput(source) else {
      throw CaptureError.message("The camera could not be opened. Close other apps using it and try again.")
    }
    capture.addInput(source)
    let frames = AVCaptureVideoDataOutput()
    frames.alwaysDiscardsLateVideoFrames = true
    frames.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange]
    frames.setSampleBufferDelegate(self, queue: callbackQueue)
    phase = "session.output.add"
    guard capture.canAddOutput(frames) else {
      throw CaptureError.message("Camera frames are unavailable. Reconnect the camera and try again.")
    }
    capture.addOutput(frames)
    phase = "device.configuration.lock"
    try device.lockForConfiguration()
    phase = "device.format.select"
    let target = CameraFrameSize.fit(width: Int(nativeSize.width), height: Int(nativeSize.height),
      maxWidth: max(profile.width, CameraPreviewBuffer.maxWidth),
      maxHeight: max(profile.height, CameraPreviewBuffer.maxHeight))
    let nativeAspect = Double(nativeSize.width) / Double(nativeSize.height)
    let formats = device.formats.filter { format in
      let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
      return size.width > 0 && size.height > 0
        && abs(Double(size.width) / Double(size.height) - nativeAspect) < 0.01
        && format.videoSupportedFrameRateRanges.contains {
          $0.minFrameRate <= Double(profile.frameRate) && $0.maxFrameRate >= Double(profile.frameRate)
        }
    }
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
      $0.minFrameRate <= Double(profile.frameRate) && $0.maxFrameRate >= Double(profile.frameRate)
    }) {
      let duration = CMTime(value: 1, timescale: CMTimeScale(profile.frameRate))
      device.activeVideoMinFrameDuration = duration
      device.activeVideoMaxFrameDuration = duration
    }
    device.unlockForConfiguration()
    diagnostics.record("device.configured", [
      "device": deviceDetails(device), "nativeWidth": nativeSize.width, "nativeHeight": nativeSize.height,
      "targetWidth": target.width, "targetHeight": target.height,
      "candidateFormats": formats.count, "suitableFormats": suitable.count, "selectedFormatFound": selected != nil,
      "minFrameDurationSeconds": CMTimeGetSeconds(device.activeVideoMinFrameDuration),
      "maxFrameDurationSeconds": CMTimeGetSeconds(device.activeVideoMaxFrameDuration),
    ])
    let sourceSize = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
    requestedProfile = profile
    self.profile = profile.fitting(width: Int(sourceSize.width), height: Int(sourceSize.height))
    self.deviceId = device.uniqueID
    if let previewPath {
      phase = "preview.mapping"
      let buffer = try CameraPreviewBuffer(path: previewPath,
        width: CameraPreviewBuffer.maxWidth, height: CameraPreviewBuffer.maxHeight)
      previewBuffer = buffer
      preview = CameraPreviewWorker { pixels, time in buffer.publish(pixels, time: time) }
    }
    session = capture
    output = frames
    observe(capture, device: device)
    phase = "session.configuration.commit"
    capture.commitConfiguration()
    configuring = false
    phase = "session.startRunning"
    let startedAt = ProcessInfo.processInfo.systemUptime
    diagnostics.record("session.startRunning.begin", ["captureId": captureId as Any? ?? NSNull()])
    capture.startRunning()
    phase = "armed"
    diagnostics.record("session.startRunning.complete", [
      "elapsedMs": (ProcessInfo.processInfo.systemUptime - startedAt) * 1000,
      "running": capture.isRunning,
    ])
    startHealthTimer()
  }

  func begin(path: String, maxBytes: Int64, runId: String? = nil) throws {
    phase = "recording.begin"
    diagnostics.record("recording.begin", ["runId": runId as Any? ?? NSNull(), "path": path, "maxBytes": maxBytes])
    guard session?.isRunning == true else {
      throw CaptureError.message("The camera is warming up. Wait a moment before starting the next run.")
    }
    guard maxBytes >= 2 * 1024 * 1024 else {
      throw CaptureError.message("Camera storage is full. Increase the storage limit or remove older videos.")
    }
    try callbackQueue.sync {
      guard writer == nil else { throw CaptureError.message("A camera recording is already running.") }
      let asset = try AVAssetWriter(outputURL: URL(fileURLWithPath: path), fileType: .mp4)
      let video = AVAssetWriterInput(mediaType: .video, outputSettings: profile.outputSettings)
      video.expectsMediaDataInRealTime = true
      guard asset.canAdd(video) else { throw CaptureError.message("H.264 camera encoding is unavailable.") }
      asset.add(video)
      asset.shouldOptimizeForNetworkUse = true
      guard asset.startWriting() else { throw asset.error ?? CaptureError.message("Video recording could not start.") }
      writer = asset
      input = video
      boundaryHostTime = CMClockGetTime(CMClockGetHostTimeClock())
      self.runId = runId
      receivedAtBegin = statistics.received
      encodedAtBegin = statistics.encoded
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
      phase = "recording"
      diagnostics.record("recording.writer.started", [
        "runId": runId as Any? ?? NSNull(), "writerStatus": asset.status.rawValue,
        "boundaryHostSeconds": CMTimeGetSeconds(boundaryHostTime), "maxDurationSeconds": maxDuration,
        "maxFileBytes": maxFileBytes, "outputSettings": profile.outputSettings,
      ])
    }
  }

  func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
    let arrival = ProcessInfo.processInfo.systemUptime
    statistics.arrived(at: arrival)
    let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
    statistics.lastPresentationTime = pts.isValid && !pts.isIndefinite ? CMTimeGetSeconds(pts) : nil
    if let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) {
      let width = CVPixelBufferGetWidth(pixels)
      let height = CVPixelBufferGetHeight(pixels)
      let format = CVPixelBufferGetPixelFormatType(pixels)
      if width != sourceWidth || height != sourceHeight || format != sourcePixelFormat {
        sourceWidth = width
        sourceHeight = height
        sourcePixelFormat = format
        diagnostics.record("capture.frame.format", [
          "captureId": captureId as Any? ?? NSNull(), "receivedFrames": statistics.received,
          "width": width, "height": height, "pixelFormat": format,
          "presentationSeconds": statistics.lastPresentationTime as Any? ?? NSNull(),
        ])
      }
      if writer == nil {
        profile = requestedProfile.fitting(width: CVPixelBufferGetWidth(pixels), height: CVPixelBufferGetHeight(pixels))
      }
      preview?.submit(pixels, time: ProcessInfo.processInfo.systemUptime)
    } else {
      statistics.missingPixels += 1
      if statistics.missingPixels == 1 { diagnostics.record("capture.frame.no-pixels") }
    }
    if let clock = session?.masterClock, pts.isValid, !pts.isIndefinite {
      let host = CMSyncConvertTime(pts, from: clock, to: CMClockGetHostTimeClock())
      if host.isValid, !host.isIndefinite {
        let delayMs = CMTimeGetSeconds(CMTimeSubtract(CMClockGetTime(CMClockGetHostTimeClock()), host)) * 1000
        statistics.lastDeliveryDelayMs = delayMs
        statistics.maxDeliveryDelayMs = max(statistics.maxDeliveryDelayMs, delayMs)
      }
    }
    if statistics.received == 1 {
      diagnostics.record("capture.first-frame", stateOnCallbackQueue())
    }
    guard let writer, let input else { return }
    guard !sizeLimited else { statistics.skip("storage-limit"); return }
    guard captureError == nil else { statistics.skip("capture-error"); return }
    guard input.isReadyForMoreMediaData else {
      if statistics.skip("encoder-backpressure") {
        diagnostics.record("recording.encoder.backpressure", ["writerStatus": writer.status.rawValue], error: writer.error)
      }
      return
    }
    guard let clock = session?.masterClock else {
      if statistics.skip("missing-capture-clock") { diagnostics.record("recording.clock.missing") }
      return
    }
    guard pts.isValid, !pts.isIndefinite else {
      if statistics.skip("invalid-presentation-time") {
        diagnostics.record("recording.timestamp.invalid", ["value": pts.value, "timescale": pts.timescale, "flags": pts.flags.rawValue])
      }
      return
    }
    let hostTime = CMSyncConvertTime(pts, from: clock, to: CMClockGetHostTimeClock())
    guard hostTime.isValid, !hostTime.isIndefinite else {
      if statistics.skip("invalid-host-time") { diagnostics.record("recording.host-time.invalid") }
      return
    }
    guard CMTimeCompare(hostTime, boundaryHostTime) >= 0 else { statistics.skip("before-recording-boundary"); return }
    if firstTime.isValid {
      guard CMTimeCompare(pts, lastTime) > 0 else { statistics.skip("non-monotonic-timestamp"); return }
      // Some cameras only deliver 30 fps even when 24 fps was requested.
      // Drop excess frames without copying pixels or changing their timestamps.
      guard !nextFrameTime.isValid || CMTimeCompare(pts, nextFrameTime) >= 0 else { statistics.skip("frame-rate-limit"); return }
      if CMTimeGetSeconds(CMTimeSubtract(pts, firstTime)) >= maxDuration {
        sizeLimited = true
        diagnostics.record("recording.limit.duration", ["maxDurationSeconds": maxDuration])
        return
      }
      if !lastSizeCheck.isValid || CMTimeGetSeconds(CMTimeSubtract(pts, lastSizeCheck)) >= 1 {
        lastSizeCheck = pts
        let bytes = (try? FileManager.default.attributesOfItem(atPath: writer.outputURL.path)[.size] as? NSNumber)?.int64Value ?? 0
        if bytes >= maxFileBytes {
          sizeLimited = true
          diagnostics.record("recording.limit.bytes", ["bytes": bytes, "maxFileBytes": maxFileBytes])
          return
        }
      }
    } else {
      writer.startSession(atSourceTime: pts)
    }
    guard input.append(sampleBuffer) else {
      captureError = writer.error ?? CaptureError.message("Camera frames could not be encoded.")
      diagnostics.record("recording.append.failed", stateOnCallbackQueue(), error: captureError)
      return
    }
    if !firstTime.isValid {
      firstTime = pts
      firstFrameHostTime = CMClockConvertHostTimeToSystemUnits(hostTime)
      diagnostics.record("recording.first-frame", [
        "runId": runId as Any? ?? NSNull(), "firstFrameHostTime": firstFrameHostTime,
        "presentationSeconds": CMTimeGetSeconds(pts), "hostSeconds": CMTimeGetSeconds(hostTime),
        "boundaryDelayMs": CMTimeGetSeconds(CMTimeSubtract(hostTime, boundaryHostTime)) * 1000,
      ])
    }
    statistics.encoded += 1
    lastTime = pts
    let nextIndex = CMTimeConvertScale(CMTimeSubtract(pts, firstTime), timescale: CMTimeScale(profile.frameRate), method: .roundTowardZero).value + 1
    nextFrameTime = CMTimeAdd(firstTime, CMTime(value: nextIndex, timescale: CMTimeScale(profile.frameRate)))
  }

  func end() throws -> CameraEndResponse {
    phase = "recording.finalize"
    diagnostics.record("recording.end.begin", diagnosticState())
    let state = callbackQueue.sync { () -> (AVAssetWriter?, Error?, UInt64, Int64, Bool, Int, Int) in
      let asset = writer
      if firstTime.isValid { input?.markAsFinished() }
      let duration = firstTime.isValid
        ? max(1, Int64((CMTimeGetSeconds(CMTimeSubtract(lastTime, firstTime)) + 1 / Double(profile.frameRate)) * 1_000_000)) : 0
      let result = (asset, captureError, firstFrameHostTime, duration, sizeLimited, profile.width, profile.height)
      writer = nil
      input = nil
      return result
    }
    if let error = state.1 { state.0?.cancelWriting(); throw error }
    if let asset = state.0 {
      if state.2 == 0 { asset.cancelWriting() }
      else {
        let done = DispatchSemaphore(value: 0)
        asset.finishWriting { done.signal() }
        guard done.wait(timeout: .now() + 20) == .success else {
          asset.cancelWriting()
          throw CaptureError.message("The camera video could not be finalized. Try reconnecting the camera.")
        }
        guard asset.status == .completed else {
          throw asset.error ?? CaptureError.message("The camera video could not be saved.")
        }
      }
    }
    phase = "armed"
    diagnostics.record("recording.end.complete", [
      "runId": runId as Any? ?? NSNull(), "firstFrameHostTime": state.2, "durationUs": state.3,
      "sizeLimited": state.4, "width": state.5, "height": state.6,
      "writerStatus": state.0?.status.rawValue as Any? ?? NSNull(),
    ])
    return CameraEndResponse(deviceId: deviceId, firstFrameHostTime: state.2, durationUs: state.3, sizeLimited: state.4,
      width: state.5, height: state.6)
  }

  func captureOutput(_ output: AVCaptureOutput, didDrop sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
    let reason = CMGetAttachment(sampleBuffer, key: kCMSampleBufferAttachmentKey_DroppedFrameReason,
      attachmentModeOut: nil).map { String(describing: $0) } ?? "unknown"
    if statistics.drop(reason) {
      diagnostics.record("capture.frame.dropped", [
        "reason": reason, "captureId": captureId as Any? ?? NSNull(),
        "presentationSeconds": CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sampleBuffer)),
      ])
    }
  }

  func diagnosticState() -> [String: Any] {
    callbackQueue.sync { stateOnCallbackQueue() }
  }

  private func stateOnCallbackQueue() -> [String: Any] {
    var state = statistics.snapshot(at: ProcessInfo.processInfo.systemUptime)
    state["captureId"] = captureId as Any? ?? NSNull()
    state["runId"] = runId as Any? ?? NSNull()
    state["phase"] = phase
    state["logPath"] = diagnostics.path
    state["authorization"] = authorizationName()
    state["sessionRunning"] = session?.isRunning ?? false
    // macOS exposes interruption notifications, but not AVCaptureSession.isInterrupted
    // or the iOS-only interruption-reason key. Preserve whatever userInfo it supplies.
    state["observedInterrupted"] = sessionInterrupted
    state["lastInterruption"] = lastInterruption as Any? ?? NSNull()
    state["lastRuntimeError"] = lastRuntimeError.map { CameraDiagnostics.errorDetails($0) } as Any? ?? NSNull()
    state["selectedDevice"] = selectedDevice.map { deviceDetails($0) } as Any? ?? NSNull()
    state["sourceWidth"] = sourceWidth
    state["sourceHeight"] = sourceHeight
    state["sourcePixelFormat"] = sourcePixelFormat
    state["recordingWidth"] = profile.width
    state["recordingHeight"] = profile.height
    state["recordingFrameRate"] = profile.frameRate
    state["recordingBitRate"] = profile.bitRate
    state["runReceivedFrames"] = statistics.received - receivedAtBegin
    state["runEncodedFrames"] = statistics.encoded - encodedAtBegin
    state["writerPresent"] = writer != nil
    state["writerStatus"] = writer?.status.rawValue as Any? ?? NSNull()
    state["writerError"] = writer?.error.map { CameraDiagnostics.errorDetails($0) } as Any? ?? NSNull()
    state["captureError"] = captureError.map { CameraDiagnostics.errorDetails($0) } as Any? ?? NSNull()
    state["firstFrameHostTime"] = firstFrameHostTime
    state["sizeLimited"] = sizeLimited
    state["previewWorker"] = preview?.diagnosticState() as Any? ?? NSNull()
    state["sharedPreview"] = previewBuffer?.diagnosticState() as Any? ?? NSNull()
    return state
  }

  private func startHealthTimer() {
    let timer = DispatchSource.makeTimerSource(queue: callbackQueue)
    timer.schedule(deadline: .now() + 5, repeating: 5, leeway: .milliseconds(250))
    timer.setEventHandler { [weak self] in
      guard let self, self.session != nil else { return }
      self.diagnostics.record("capture.health", self.stateOnCallbackQueue())
      self.statistics.lastHealthTime = ProcessInfo.processInfo.systemUptime
      self.statistics.lastHealthFrames = self.statistics.received
    }
    healthTimer = timer
    timer.resume()
  }

  private func observe(_ capture: AVCaptureSession, device: AVCaptureDevice) {
    let names: [Notification.Name] = [
      AVCaptureSession.didStartRunningNotification, AVCaptureSession.didStopRunningNotification,
      AVCaptureSession.runtimeErrorNotification, AVCaptureSession.wasInterruptedNotification,
      AVCaptureSession.interruptionEndedNotification,
    ]
    for name in names {
      observations.append(NotificationCenter.default.addObserver(forName: name, object: capture, queue: nil) {
        [weak self, weak capture] notification in
        guard let self, let capture else { return }
        let info = Dictionary(uniqueKeysWithValues: (notification.userInfo ?? [:]).map { (String(describing: $0.key), $0.value) })
        let nativeError = notification.userInfo?[AVCaptureSessionErrorKey] as? NSError
        self.callbackQueue.async {
          guard self.session === capture else { return }
          if name == AVCaptureSession.runtimeErrorNotification { self.lastRuntimeError = nativeError }
          if name == AVCaptureSession.wasInterruptedNotification {
            self.sessionInterrupted = true
            self.lastInterruption = info
          }
          if name == AVCaptureSession.interruptionEndedNotification { self.sessionInterrupted = false }
          var state = self.stateOnCallbackQueue()
          state["notification"] = name.rawValue
          state["userInfo"] = info
          self.diagnostics.record("session.notification", state, error: nativeError)
        }
      })
    }
    for name in [AVCaptureDevice.wasConnectedNotification, AVCaptureDevice.wasDisconnectedNotification] {
      observations.append(NotificationCenter.default.addObserver(forName: name, object: nil, queue: nil) {
        [weak self] notification in
        guard let self, let changed = notification.object as? AVCaptureDevice,
          changed.hasMediaType(.video) else { return }
        self.diagnostics.record("device.notification", [
          "notification": name.rawValue, "device": self.deviceDetails(changed),
          "selected": changed.uniqueID == device.uniqueID,
        ])
      })
    }
  }

  private func deviceDetails(_ device: AVCaptureDevice) -> [String: Any] {
    let format = device.activeFormat
    let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
    return [
      "id": device.uniqueID, "name": device.localizedName, "modelId": device.modelID,
      "type": device.deviceType.rawValue, "connected": device.isConnected, "suspended": device.isSuspended,
      "inUseByAnotherApplication": device.isInUseByAnotherApplication,
      "activeWidth": size.width, "activeHeight": size.height,
      "activePixelFormat": CMFormatDescriptionGetMediaSubType(format.formatDescription),
      "supportedFrameRateRanges": format.videoSupportedFrameRateRanges.map { ["min": $0.minFrameRate, "max": $0.maxFrameRate] },
    ]
  }

  private func authorizationName() -> String {
    switch AVCaptureDevice.authorizationStatus(for: .video) {
    case .notDetermined: return "notDetermined"
    case .restricted: return "restricted"
    case .denied: return "denied"
    case .authorized: return "authorized"
    @unknown default: return "unknown"
    }
  }

  func disarm() {
    if session != nil { diagnostics.record("capture.disarm.begin", diagnosticState()) }
    healthTimer?.cancel()
    healthTimer = nil
    callbackQueue.sync {
      writer?.cancelWriting()
      writer = nil
      input = nil
      preview?.stop()
      preview = nil
      previewBuffer = nil
    }
    session?.stopRunning()
    for observation in observations { NotificationCenter.default.removeObserver(observation) }
    observations.removeAll()
    output?.setSampleBufferDelegate(nil, queue: nil)
    output = nil
    session = nil
    deviceId = nil
    selectedDevice = nil
    phase = "idle"
    diagnostics.record("capture.disarm.complete", ["captureId": captureId as Any? ?? NSNull()])
  }

  private func ensurePermission() throws {
    diagnostics.record("permission.check", ["authorization": authorizationName()])
    if AVCaptureDevice.authorizationStatus(for: .video) == .notDetermined {
      let completed = DispatchSemaphore(value: 0)
      DispatchQueue.main.async {
        self.diagnostics.record("permission.prompt")
        NSApplication.shared.activate(ignoringOtherApps: true)
        AVCaptureDevice.requestAccess(for: .video) { granted in
          self.diagnostics.record("permission.answer", ["granted": granted, "authorization": self.authorizationName()])
          completed.signal()
          DispatchQueue.main.async { NSApplication.shared.hide(nil) }
        }
      }
      guard completed.wait(timeout: .now() + 120) == .success else {
        diagnostics.record("permission.timeout", ["timeoutSeconds": 120, "authorization": authorizationName()])
        throw CaptureError.message("Camera permission is still pending. Allow camera access and try again.")
      }
    }
    guard AVCaptureDevice.authorizationStatus(for: .video) == .authorized else {
      throw CaptureError.message("Camera access is off. Allow TUFReplay Microphone Capture in System Settings > Privacy & Security > Camera.")
    }
    diagnostics.record("permission.ready", ["authorization": authorizationName()])
  }
}
