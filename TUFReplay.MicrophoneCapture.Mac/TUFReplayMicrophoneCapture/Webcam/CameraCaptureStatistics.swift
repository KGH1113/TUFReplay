import Foundation

// Accessed only by WebcamCaptureService.callbackQueue. No log formatting per frame.
struct CameraCaptureStatistics {
  var received: Int64 = 0
  var missingPixels: Int64 = 0
  var encoded: Int64 = 0
  var drops: [String: Int64] = [:]
  var skips: [String: Int64] = [:]
  var firstArrival: Double?
  var lastArrival: Double?
  var maxArrivalGap = 0.0
  var lastPresentationTime: Double?
  var lastDeliveryDelayMs: Double?
  var maxDeliveryDelayMs = 0.0
  var lastHealthTime: Double?
  var lastHealthFrames: Int64 = 0

  mutating func arrived(at now: Double) {
    received += 1
    if let lastArrival { maxArrivalGap = max(maxArrivalGap, now - lastArrival) }
    if firstArrival == nil { firstArrival = now }
    lastArrival = now
  }

  @discardableResult
  mutating func skip(_ reason: String) -> Bool {
    skips[reason, default: 0] += 1
    return skips[reason] == 1
  }

  mutating func drop(_ reason: String) -> Bool {
    let key = drops[reason] != nil || drops.count < 16 ? reason : "other"
    drops[key, default: 0] += 1
    return drops[key] == 1
  }

  func snapshot(at now: Double) -> [String: Any] {
    let interval = now - (lastHealthTime ?? firstArrival ?? now)
    return [
      "receivedFrames": received, "encodedFrames": encoded, "missingPixelBuffers": missingPixels,
      "droppedFrames": drops, "encodingSkips": skips,
      "firstArrivalUptime": firstArrival as Any? ?? NSNull(),
      "lastFrameAgeSeconds": lastArrival.map { now - $0 } as Any? ?? NSNull(),
      "maxArrivalGapSeconds": maxArrivalGap,
      "receivedFpsSinceLastHealth": interval > 0 ? Double(received - lastHealthFrames) / interval : 0,
      "lastPresentationSeconds": lastPresentationTime as Any? ?? NSNull(),
      "lastDeliveryDelayMs": lastDeliveryDelayMs as Any? ?? NSNull(),
      "maxDeliveryDelayMs": maxDeliveryDelayMs,
    ]
  }
}
