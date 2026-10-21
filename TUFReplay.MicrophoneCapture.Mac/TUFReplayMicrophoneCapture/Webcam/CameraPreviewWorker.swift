import CoreVideo
import Foundation

// Capture and encoding must not wait for preview conversion. Retain only the
// frame being rendered and the latest pending frame, never a backlog of frames.
final class CameraPreviewWorker {
  private let queue: DispatchQueue
  private let render: (CVPixelBuffer, Double) -> Void
  private let gate = NSLock()
  private var pending: (CVPixelBuffer, Double)?
  private var scheduled = false
  private var stopped = false

  init(queue: DispatchQueue = DispatchQueue(label: "impl.tufreplay.webcam.preview", qos: .userInitiated),
    render: @escaping (CVPixelBuffer, Double) -> Void) {
    self.queue = queue
    self.render = render
  }

  func submit(_ pixels: CVPixelBuffer, time: Double) {
    gate.lock()
    guard !stopped else { gate.unlock(); return }
    pending = (pixels, time)
    let start = !scheduled
    scheduled = true
    gate.unlock()
    if start { queue.async { self.drain() } }
  }

  func stop() {
    gate.lock()
    stopped = true
    pending = nil
    gate.unlock()
  }

  private func drain() {
    while true {
      gate.lock()
      guard !stopped, let frame = pending else {
        scheduled = false
        gate.unlock()
        return
      }
      pending = nil
      gate.unlock()
      // Release Core Image intermediates after each frame even while new
      // camera frames keep the worker continuously active.
      autoreleasepool { render(frame.0, frame.1) }
    }
  }
}
