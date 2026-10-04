import AVFoundation
import CoreVideo

enum WebcamCaptureSelfTest {
  static func run() throws {
    try CameraDiagnosticsSelfTest.run()
    let profile = WebcamProfile(width: 640, height: 480, frameRate: 30, bitRate: 600_000).fitting(width: 1920, height: 1080)
    try profile.validate()
    guard profile.width == 640, profile.height == 360 else {
      throw CaptureError.message("Compact camera capture did not preserve its native aspect ratio.")
    }
    let fourByThree = WebcamProfile(width: 1280, height: 720, frameRate: 30, bitRate: 1_200_000).fitting(width: 1600, height: 1200)
    guard fourByThree.width == 960, fourByThree.height == 720 else {
      throw CaptureError.message("Balanced camera capture did not preserve a 4:3 aspect ratio.")
    }
    try WebcamProfile(width: 1920, height: 1080, frameRate: 30, bitRate: 3_000_000).validate()
    let url = FileManager.default.temporaryDirectory.appendingPathComponent("tufreplay-webcam-self-test-\(UUID().uuidString).mp4")
    defer { try? FileManager.default.removeItem(at: url) }
    let prepared = try CameraPreparedWriter(directory: FileManager.default.temporaryDirectory.path, profile: profile)
    defer { prepared.cancel() }
    let writer = prepared.writer
    let input = prepared.input
    let standbyURL = writer.outputURL
    guard writer.status == .writing, !FileManager.default.fileExists(atPath: url.path) else {
      throw CaptureError.message("Camera writer was not ready before attaching a run.")
    }
    let sourceWidth = 1280
    let sourceHeight = 720
    var pixelBuffer: CVPixelBuffer?
    guard CVPixelBufferCreate(kCFAllocatorDefault, sourceWidth, sourceHeight, kCVPixelFormatType_32BGRA, nil, &pixelBuffer) == kCVReturnSuccess,
      let pixelBuffer else { throw CaptureError.message("Synthetic camera frame allocation failed.") }
    CVPixelBufferLockBaseAddress(pixelBuffer, [])
    if let address = CVPixelBufferGetBaseAddress(pixelBuffer) {
      let bytes = address.assumingMemoryBound(to: UInt8.self)
      let rowBytes = CVPixelBufferGetBytesPerRow(pixelBuffer)
      for y in 0..<sourceHeight {
        for x in 0..<sourceWidth {
          let offset = y * rowBytes + x * 4
          let top = y < sourceHeight / 2
          let left = x < sourceWidth / 2
          bytes[offset] = !top ? 255 : 0
          bytes[offset + 1] = !left ? 255 : 0
          bytes[offset + 2] = (top && left) || (!top && !left) ? 255 : 0
          bytes[offset + 3] = 255
        }
      }
    }
    CVPixelBufferUnlockBaseAddress(pixelBuffer, [])
    var description: CMVideoFormatDescription?
    guard CMVideoFormatDescriptionCreateForImageBuffer(allocator: kCFAllocatorDefault,
      imageBuffer: pixelBuffer, formatDescriptionOut: &description) == noErr, let description else {
      throw CaptureError.message("Synthetic camera frame description failed.")
    }
    let previewURL = FileManager.default.temporaryDirectory.appendingPathComponent("tufreplay-camera-preview-test-\(UUID().uuidString).frame")
    defer { try? FileManager.default.removeItem(at: previewURL) }
    let previewBytes = 64 + CameraPreviewBuffer.maxWidth * CameraPreviewBuffer.maxHeight * 4
    try Data(count: previewBytes).write(to: previewURL)
    let preview = try CameraPreviewBuffer(path: previewURL.path,
      width: CameraPreviewBuffer.maxWidth, height: CameraPreviewBuffer.maxHeight)
    preview.publish(pixelBuffer, time: 1)
    let shared = try Data(contentsOf: previewURL)
    guard shared.withUnsafeBytes({ $0.loadUnaligned(as: Int64.self) }) == 2,
      shared.count == previewBytes,
      shared.withUnsafeBytes({ $0.loadUnaligned(fromByteOffset: 8, as: Int32.self) }) == 960,
      shared.withUnsafeBytes({ $0.loadUnaligned(fromByteOffset: 12, as: Int32.self) }) == 540 else {
      throw CaptureError.message("Shared camera preview dimensions were not preserved.")
    }
    let bottomLeft = 64 + (10 * 960 + 10) * 4
    let topLeft = 64 + (530 * 960 + 10) * 4
    let topRight = 64 + (530 * 960 + 950) * 4
    guard shared[bottomLeft] < 5, shared[bottomLeft + 2] > 250,
      shared[topLeft] > 250, shared[topLeft + 2] < 5,
      shared[topRight] < 5, shared[topRight + 1] > 250 else {
      throw CaptureError.message("Camera preview was flipped or its colors changed.")
    }
    preview.publish(pixelBuffer, time: 1.01)
    guard try Data(contentsOf: previewURL).withUnsafeBytes({ $0.loadUnaligned(as: Int64.self) }) == 2 else {
      throw CaptureError.message("Camera preview exceeded its 30 fps cadence.")
    }
    preview.publish(pixelBuffer, time: 1.04)
    guard try Data(contentsOf: previewURL).withUnsafeBytes({ $0.loadUnaligned(as: Int64.self) }) == 2 else {
      throw CaptureError.message("Hidden camera previews kept rendering unused frames.")
    }
    let demand = try FileHandle(forUpdating: previewURL)
    try demand.seek(toOffset: 16)
    var requested: Int32 = 1
    try demand.write(contentsOf: withUnsafeBytes(of: &requested) { Data($0) })
    try demand.close()
    for frame in 1..<30 {
      preview.publish(pixelBuffer, time: 1 + Double(frame) / 30 + (frame % 2 == 0 ? 0.002 : -0.002))
    }
    guard try Data(contentsOf: previewURL).withUnsafeBytes({ $0.loadUnaligned(as: Int64.self) }) == 60 else {
      throw CaptureError.message("Camera preview dropped 30 fps frames due to timing jitter.")
    }
    var portrait: CVPixelBuffer?
    guard CVPixelBufferCreate(kCFAllocatorDefault, 720, 960, kCVPixelFormatType_32BGRA, nil, &portrait) == kCVReturnSuccess,
      let portrait else { throw CaptureError.message("Portrait camera frame allocation failed.") }
    preview.publish(portrait, time: 2)
    let portraitFrame = try Data(contentsOf: previewURL)
    guard portraitFrame.count == previewBytes,
      portraitFrame.withUnsafeBytes({ $0.loadUnaligned(fromByteOffset: 8, as: Int32.self) }) == 540,
      portraitFrame.withUnsafeBytes({ $0.loadUnaligned(fromByteOffset: 12, as: Int32.self) }) == 720 else {
      throw CaptureError.message("Portrait camera preview changed its aspect ratio or memory budget.")
    }
    try validatePreviewDownscaling(preview: preview, url: previewURL)
    try validatePreviewVideoLevels(preview: preview, url: previewURL)
    try validatePreviewWorker(pixels: pixelBuffer)
    if ProcessInfo.processInfo.environment["TUFREPLAY_CAMERA_PREVIEW_BENCHMARK"] == "1" {
      try benchmarkPreview(preview: preview)
    }
    // Idle preview work above must not become recorded video. Start its clock
    // only when the first synthetic run attaches to the prepared writer.
    writer.startSession(atSourceTime: .zero)
    for frame in 0..<12 {
      var timing = CMSampleTimingInfo(duration: CMTime(value: 1, timescale: 30),
        presentationTimeStamp: CMTime(value: Int64(frame), timescale: 30), decodeTimeStamp: .invalid)
      var sample: CMSampleBuffer?
      guard CMSampleBufferCreateReadyWithImageBuffer(allocator: kCFAllocatorDefault, imageBuffer: pixelBuffer,
        formatDescription: description, sampleTiming: &timing, sampleBufferOut: &sample) == noErr, let sample else {
        throw CaptureError.message("Synthetic camera sample creation failed.")
      }
      let deadline = Date().addingTimeInterval(5)
      while !input.isReadyForMoreMediaData && writer.status == .writing && Date() < deadline {
        Thread.sleep(forTimeInterval: 0.001)
      }
      guard input.isReadyForMoreMediaData,
        input.append(sample) else {
        writer.cancelWriting()
        throw writer.error ?? CaptureError.message("Synthetic camera frame encoding failed.")
      }
    }
    input.markAsFinished()
    try prepared.finish(destination: url.path)
    guard !FileManager.default.fileExists(atPath: standbyURL.path) else {
      throw CaptureError.message("The camera standby destination was not released after recording.")
    }
    let unused = try CameraPreparedWriter(directory: FileManager.default.temporaryDirectory.path, profile: profile)
    let unusedURL = unused.writer.outputURL
    unused.cancel()
    guard !FileManager.default.fileExists(atPath: unusedURL.path) else {
      throw CaptureError.message("An unused camera writer leaked its temporary file.")
    }
    let boundary = CMTime(seconds: 10, preferredTimescale: 1_000_000_000)
    guard WebcamCaptureService.canReuseLatestSample(hostTime: CMTime(seconds: 9.94, preferredTimescale: 1_000_000_000), boundary: boundary),
      !WebcamCaptureService.canReuseLatestSample(hostTime: CMTime(seconds: 9, preferredTimescale: 1_000_000_000), boundary: boundary),
      !WebcamCaptureService.canReuseLatestSample(hostTime: CMTime(seconds: 10.01, preferredTimescale: 1_000_000_000), boundary: boundary),
      !WebcamCaptureService.canReuseLatestSample(hostTime: .invalid, boundary: boundary) else {
      throw CaptureError.message("Camera run attachment accepted an invalid or stale frame.")
    }
    let asset = AVURLAsset(url: url)
    guard let track = asset.tracks(withMediaType: .video).first,
      track.naturalSize == CGSize(width: 640, height: 360), asset.duration.seconds > 0.35 else {
      throw CaptureError.message("Synthetic camera MP4 metadata is invalid.")
    }
    let reader = try AVAssetReader(asset: asset)
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: [
      kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA
    ])
    reader.add(output)
    guard reader.startReading() else { throw CaptureError.message("Synthetic camera decoding could not start.") }
    var count = 0
    while output.copyNextSampleBuffer() != nil { count += 1 }
    guard count == 12, reader.status == .completed else {
      throw CaptureError.message("Synthetic camera H.264 video did not decode all frames.")
    }
    let command = try JSONDecoder().decode(CommandRequest.self,
      from: Data(#"{"command":"cameraBegin","path":"test.mp4","maxBytes":134217728}"#.utf8))
    guard command.command == CommandName.cameraBegin.rawValue, command.maxBytes == 134217728 else {
      throw CaptureError.message("Camera command protocol validation failed.")
    }
    let armCommand = try JSONDecoder().decode(CommandRequest.self,
      from: Data(#"{"command":"cameraArm","recordingDirectory":"/tmp/camera-recordings"}"#.utf8))
    guard armCommand.command == CommandName.cameraArm.rawValue, armCommand.recordingDirectory == "/tmp/camera-recordings" else {
      throw CaptureError.message("The camera standby recording directory was not preserved by the protocol.")
    }
    let response = CameraEndResponse(deviceId: "camera", firstFrameHostTime: 9_007_199_254_740_993, durationUs: 400_000,
      sizeLimited: false, width: profile.width, height: profile.height)
    struct Timestamp: Decodable { let firstFrameHostTime: UInt64 }
    let decoded = try JSONDecoder().decode(Timestamp.self, from: Data(try JsonLineCodec.encode(response).utf8))
    guard decoded.firstFrameHostTime == response.firstFrameHostTime else {
      throw CaptureError.message("The camera host timestamp lost precision.")
    }
  }

  private static func validatePreviewDownscaling(preview: CameraPreviewBuffer, url: URL) throws {
    var pixels: CVPixelBuffer?
    guard CVPixelBufferCreate(kCFAllocatorDefault, 1920, 1440, kCVPixelFormatType_32BGRA, nil, &pixels) == kCVReturnSuccess,
      let pixels else { throw CaptureError.message("Detailed camera frame allocation failed.") }
    CVPixelBufferLockBaseAddress(pixels, [])
    if let address = CVPixelBufferGetBaseAddress(pixels) {
      let bytes = address.assumingMemoryBound(to: UInt8.self)
      let rowBytes = CVPixelBufferGetBytesPerRow(pixels)
      for y in 0..<1440 {
        for x in 0..<1920 {
          let offset = y * rowBytes + x * 4
          let value: UInt8 = (x + y) % 2 == 0 ? 0 : 255
          bytes[offset] = value
          bytes[offset + 1] = value
          bytes[offset + 2] = value
          bytes[offset + 3] = 255
        }
      }
    }
    CVPixelBufferUnlockBaseAddress(pixels, [])
    preview.publish(pixels, time: 3)
    let shared = try Data(contentsOf: url)
    for y in [100, 300, 600] {
      for x in [100, 400, 800] {
        let offset = 64 + (y * 960 + x) * 4
        guard shared[offset] > 80, shared[offset] < 220,
          abs(Int(shared[offset]) - Int(shared[offset + 1])) < 3,
          shared[offset + 3] == 255 else {
          throw CaptureError.message("Camera preview downscaling aliased fine detail or lost opacity.")
        }
      }
    }
  }

  private static func validatePreviewVideoLevels(preview: CameraPreviewBuffer, url: URL) throws {
    var pixels: CVPixelBuffer?
    guard CVPixelBufferCreate(kCFAllocatorDefault, 16, 16, kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange, nil, &pixels) == kCVReturnSuccess,
      let pixels else { throw CaptureError.message("Video-range camera frame allocation failed.") }
    CVBufferSetAttachment(pixels, kCVImageBufferYCbCrMatrixKey, kCVImageBufferYCbCrMatrix_ITU_R_709_2, .shouldPropagate)
    CVBufferSetAttachment(pixels, kCVImageBufferColorPrimariesKey, kCVImageBufferColorPrimaries_ITU_R_709_2, .shouldPropagate)
    CVBufferSetAttachment(pixels, kCVImageBufferTransferFunctionKey, kCVImageBufferTransferFunction_ITU_R_709_2, .shouldPropagate)
    let colors: [(UInt8, UInt8, UInt8)] = [
      (16, 128, 128), (32, 128, 128), (64, 128, 128), (128, 128, 128), (192, 128, 128), (235, 128, 128),
      (63, 102, 240), (173, 42, 26), (32, 240, 118),
    ]
    for (index, color) in colors.enumerated() {
      CVPixelBufferLockBaseAddress(pixels, [])
      let luma = CVPixelBufferGetBaseAddressOfPlane(pixels, 0)!.assumingMemoryBound(to: UInt8.self)
      luma.update(repeating: color.0,
        count: CVPixelBufferGetBytesPerRowOfPlane(pixels, 0) * CVPixelBufferGetHeightOfPlane(pixels, 0))
      let chroma = CVPixelBufferGetBaseAddressOfPlane(pixels, 1)!.assumingMemoryBound(to: UInt8.self)
      let chromaBytes = CVPixelBufferGetBytesPerRowOfPlane(pixels, 1) * CVPixelBufferGetHeightOfPlane(pixels, 1)
      for offset in stride(from: 0, to: chromaBytes, by: 2) {
        chroma[offset] = color.1
        chroma[offset + 1] = color.2
      }
      CVPixelBufferUnlockBaseAddress(pixels, [])
      preview.publish(pixels, time: 4 + Double(index) / 30)
      let shared = try Data(contentsOf: url)
      let y = (Double(color.0) - 16) * 255 / 219
      let u = (Double(color.1) - 128) * 255 / 224
      let v = (Double(color.2) - 128) * 255 / 224
      let expected = [y + 1.5748 * v, y - 0.187324 * u - 0.468124 * v, y + 1.8556 * u]
      for channel in 0..<3 {
        let value = max(0, min(255, Int(expected[channel].rounded())))
        guard abs(Int(shared[64 + channel]) - value) <= 2 else {
          throw CaptureError.message("Camera preview changed video levels, gamma, or its YCbCr color matrix.")
        }
      }
      guard shared[67] == 255 else { throw CaptureError.message("Camera preview lost its opacity.") }
    }
  }

  private static func validatePreviewWorker(pixels: CVPixelBuffer) throws {
    let queue = DispatchQueue(label: "impl.tufreplay.webcam.preview.self-test")
    let started = DispatchSemaphore(value: 0)
    let release = DispatchSemaphore(value: 0)
    var times: [Double] = []
    let worker = CameraPreviewWorker(queue: queue) { _, time in
      times.append(time)
      if time == 1 {
        started.signal()
        _ = release.wait(timeout: .now() + 5)
      }
    }
    defer { worker.stop(); release.signal() }
    worker.submit(pixels, time: 1)
    guard started.wait(timeout: .now() + 5) == .success else {
      throw CaptureError.message("Camera preview worker did not start.")
    }
    // A deliberately stalled renderer must not stop capture submissions or
    // accumulate every intervening sample. Only the newest sample survives.
    for time in 2...100 { worker.submit(pixels, time: Double(time)) }
    let blocked = worker.diagnosticState()
    guard blocked["submittedFrames"] as? Int64 == 100,
      blocked["replacedPendingFrames"] as? Int64 == 98,
      blocked["renderCompletedCalls"] as? Int64 == 0,
      blocked["inFlightAgeSeconds"] as? Double != nil else {
      throw CaptureError.message("Camera preview diagnostics could not describe a stalled worker.")
    }
    release.signal()
    queue.sync {}
    guard times == [1, 100] else {
      throw CaptureError.message("Slow camera previews blocked capture or built up stale frames.")
    }
    guard worker.diagnosticState()["renderCompletedCalls"] as? Int64 == 2 else {
      throw CaptureError.message("Camera preview diagnostics lost completed renders.")
    }
    worker.stop()
    worker.submit(pixels, time: 101)
    queue.sync {}
    guard times == [1, 100] else {
      throw CaptureError.message("Stopped camera preview workers kept accepting frames.")
    }

    let stopStarted = DispatchSemaphore(value: 0)
    let stopRelease = DispatchSemaphore(value: 0)
    var stoppedTimes: [Double] = []
    let stopping = CameraPreviewWorker(queue: queue) { _, time in
      stoppedTimes.append(time)
      stopStarted.signal()
      _ = stopRelease.wait(timeout: .now() + 5)
    }
    defer { stopping.stop(); stopRelease.signal() }
    stopping.submit(pixels, time: 1)
    guard stopStarted.wait(timeout: .now() + 5) == .success else {
      throw CaptureError.message("Camera preview stop test did not start.")
    }
    stopping.submit(pixels, time: 2)
    stopping.stop()
    stopRelease.signal()
    queue.sync {}
    guard stoppedTimes == [1] else {
      throw CaptureError.message("Camera preview shutdown rendered pending frames.")
    }
  }

  private static func benchmarkPreview(preview: CameraPreviewBuffer) throws {
    var pixels: CVPixelBuffer?
    let attributes: [String: Any] = [kCVPixelBufferIOSurfacePropertiesKey as String: [:]]
    guard CVPixelBufferCreate(kCFAllocatorDefault, 1280, 720, kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange,
      attributes as CFDictionary, &pixels) == kCVReturnSuccess, let pixels else {
      throw CaptureError.message("Camera benchmark frame allocation failed.")
    }
    CVBufferSetAttachment(pixels, kCVImageBufferYCbCrMatrixKey, kCVImageBufferYCbCrMatrix_ITU_R_709_2, .shouldPropagate)
    CVPixelBufferLockBaseAddress(pixels, [])
    for plane in 0..<2 {
      let address = CVPixelBufferGetBaseAddressOfPlane(pixels, plane)!
      memset(address, 128, CVPixelBufferGetBytesPerRowOfPlane(pixels, plane) * CVPixelBufferGetHeightOfPlane(pixels, plane))
    }
    CVPixelBufferUnlockBaseAddress(pixels, [])
    var milliseconds: [Double] = []
    for frame in 0..<100 {
      let start = DispatchTime.now().uptimeNanoseconds
      autoreleasepool { preview.publish(pixels, time: 10 + Double(frame) / 30) }
      if frame >= 10 {
        milliseconds.append(Double(DispatchTime.now().uptimeNanoseconds - start) / 1_000_000)
      }
    }
    milliseconds.sort()
    let average = milliseconds.reduce(0, +) / Double(milliseconds.count)
    let p95 = milliseconds[Int(Double(milliseconds.count - 1) * 0.95)]
    print(String(format: "Camera preview benchmark: NV12 1280x720 -> RGBA 960x540, mean %.3f ms, p95 %.3f ms, max %.3f ms",
      average, p95, milliseconds.last!))
  }
}
