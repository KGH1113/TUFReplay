import CoreImage
import CoreVideo
import Darwin

struct CameraFrameSize {
  let width: Int
  let height: Int

  static func fit(width: Int, height: Int, maxWidth: Int, maxHeight: Int) -> CameraFrameSize {
    let scale = min(1, min(Double(maxWidth) / Double(width), Double(maxHeight) / Double(height)))
    return CameraFrameSize(width: max(2, Int(Double(width) * scale) / 2 * 2),
      height: max(2, Int(Double(height) * scale) / 2 * 2))
  }
}

// One shared RGBA frame, refreshed at up to 30 fps independently of recording.
// A sequence counter lets Unity skip frames while the helper is writing.
final class CameraPreviewBuffer {
  static let maxWidth = 960
  static let maxHeight = 720
  private let pointer: UnsafeMutableRawPointer
  private let renderedFrame: UnsafeMutableRawPointer
  private let length: Int
  private let width: Int
  private let height: Int
  // Keep the camera's non-linear video values. Color-managed Rec.709 -> sRGB
  // rendering changes midtones; the preview consumer expects video RGB bytes.
  // Core Image still handles the NV12 range and YCbCr matrix attachments.
  // Game Mode deprioritizes the helper's GPU work. Do this small preview render
  // on the CPU so it never waits behind the foreground game's GPU commands.
  private let context = CIContext(options: [
    .useSoftwareRenderer: true, .cacheIntermediates: false, .workingColorSpace: NSNull(),
  ])
  private var sequence: Int64 = 0
  private var timeAnchor: Double?
  private var lastFrameIndex: Int64 = -1

  init(path: String, width: Int, height: Int) throws {
    guard width > 0, width <= Self.maxWidth, height > 0, height <= Self.maxHeight else {
      throw CaptureError.message("Invalid camera preview size.")
    }
    self.width = width
    self.height = height
    length = 64 + width * height * 4
    CameraDiagnostics.shared.record("preview.mapping.open", ["path": path, "expectedBytes": length])
    let descriptor = open(path, O_RDWR | O_NOFOLLOW)
    guard descriptor >= 0 else { throw CaptureError.posix("The camera preview could not be opened.") }
    defer { close(descriptor) }
    guard fchmod(descriptor, S_IRUSR | S_IWUSR) == 0 else {
      throw CaptureError.posix("The camera preview could not be protected.")
    }
    var info = stat()
    guard fstat(descriptor, &info) == 0 else {
      throw CaptureError.posix("The camera preview metadata could not be read.")
    }
    guard info.st_size == length, (info.st_mode & S_IFMT) == S_IFREG else {
      CameraDiagnostics.shared.record("preview.mapping.invalid", [
        "path": path, "expectedBytes": length, "actualBytes": info.st_size, "mode": info.st_mode,
      ])
      throw CaptureError.message("Invalid camera preview buffer.")
    }
    let mapped = mmap(nil, length, PROT_READ | PROT_WRITE, MAP_SHARED, descriptor, 0)
    guard let mapped, mapped != MAP_FAILED else { throw CaptureError.posix("Camera preview memory is unavailable.") }
    pointer = mapped
    renderedFrame = .allocate(byteCount: width * height * 4, alignment: 64)
    CameraDiagnostics.shared.record("preview.mapping.ready", ["path": path, "bytes": length])
  }

  func publish(_ pixelBuffer: CVPixelBuffer, time: Double) {
    // Keep capture warm for recording while avoiding RGB rendering when no
    // game overlay or setup dialog consumes the preview. Always publish the
    // first frame so readiness does not depend on overlay visibility.
    OSMemoryBarrier()
    guard sequence == 0 || pointer.advanced(by: 16).load(as: Int32.self) != 0 else { return }
    if timeAnchor == nil { timeAnchor = time }
    let frameIndex = Int64(((time - (timeAnchor ?? time)) * 30).rounded())
    guard frameIndex > lastFrameIndex else { return }
    lastFrameIndex = frameIndex
    let image = CIImage(cvPixelBuffer: pixelBuffer)
    let size = CameraFrameSize.fit(width: CVPixelBufferGetWidth(pixelBuffer), height: CVPixelBufferGetHeight(pixelBuffer),
      maxWidth: width, maxHeight: height)
    let scale = min(CGFloat(size.width) / image.extent.width, CGFloat(size.height) / image.extent.height)
    let bounds = CGRect(x: 0, y: 0, width: size.width, height: size.height)
    let scaled = scale < 1 ? image.clampedToExtent().applyingFilter("CILanczosScaleTransform", parameters: [
      kCIInputScaleKey: scale, kCIInputAspectRatioKey: 1,
    ]) : image
    // Render privately before publishing so a slow render never makes
    // the shared frame unavailable. Unity's raw textures use bottom-up rows.
    let transform = CGAffineTransform(a: 1, b: 0, c: 0, d: -1, tx: 0, ty: CGFloat(size.height))
    context.render(scaled.cropped(to: bounds).transformed(by: transform), toBitmap: renderedFrame,
      rowBytes: size.width * 4, bounds: bounds, format: .RGBA8, colorSpace: nil)
    pointer.storeBytes(of: sequence + 1, as: Int64.self)
    OSMemoryBarrier()
    pointer.advanced(by: 8).storeBytes(of: Int32(size.width), as: Int32.self)
    pointer.advanced(by: 12).storeBytes(of: Int32(size.height), as: Int32.self)
    memcpy(pointer.advanced(by: 64), renderedFrame, size.width * size.height * 4)
    OSMemoryBarrier()
    sequence += 2
    pointer.storeBytes(of: sequence, as: Int64.self)
  }

  func diagnosticState() -> [String: Any] {
    OSMemoryBarrier()
    let before = pointer.load(as: Int64.self)
    let frameWidth = pointer.advanced(by: 8).load(as: Int32.self)
    let frameHeight = pointer.advanced(by: 12).load(as: Int32.self)
    let requested = pointer.advanced(by: 16).load(as: Int32.self)
    OSMemoryBarrier()
    let after = pointer.load(as: Int64.self)
    return [
      "sequence": after, "publishedFrames": after / 2,
      "width": frameWidth, "height": frameHeight, "requested": requested != 0,
      "stable": before == after && after % 2 == 0,
    ]
  }

  deinit {
    renderedFrame.deallocate()
    munmap(pointer, length)
  }
}
