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
  private var submitted: Int64 = 0
  private var replaced: Int64 = 0
  private var rendered: Int64 = 0
  private var renderStartedAt: Double?
  private var lastCompletedAt: Double?
  private var lastRenderMs = 0.0
  private var maxRenderMs = 0.0

  init(queue: DispatchQueue = DispatchQueue(label: "impl.tufreplay.webcam.preview", qos: .userInitiated),
    render: @escaping (CVPixelBuffer, Double) -> Void) {
    self.queue = queue
    self.render = render
  }

  func submit(_ pixels: CVPixelBuffer, time: Double) {
    gate.lock()
    guard !stopped else { gate.unlock(); return }
    submitted += 1
    if pending != nil { replaced += 1 }
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

  func diagnosticState() -> [String: Any] {
    let now = ProcessInfo.processInfo.systemUptime
    gate.lock()
    defer { gate.unlock() }
    return [
      "submittedFrames": submitted, "replacedPendingFrames": replaced, "renderCompletedCalls": rendered,
      "pending": pending != nil, "stopped": stopped,
      "inFlightAgeSeconds": renderStartedAt.map { now - $0 } as Any? ?? NSNull(),
      "lastCompletedAgeSeconds": lastCompletedAt.map { now - $0 } as Any? ?? NSNull(),
      "lastRenderMs": lastRenderMs, "maxRenderMs": maxRenderMs,
    ]
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
      let startedAt = ProcessInfo.processInfo.systemUptime
      renderStartedAt = startedAt
      gate.unlock()
      // Release Core Image intermediates after each frame even while new
      // camera frames keep the worker continuously active.
      autoreleasepool { render(frame.0, frame.1) }
      let completedAt = ProcessInfo.processInfo.systemUptime
      gate.lock()
      rendered += 1
      lastRenderMs = (completedAt - startedAt) * 1000
      maxRenderMs = max(maxRenderMs, lastRenderMs)
      lastCompletedAt = completedAt
      renderStartedAt = nil
      gate.unlock()
    }
  }
}
