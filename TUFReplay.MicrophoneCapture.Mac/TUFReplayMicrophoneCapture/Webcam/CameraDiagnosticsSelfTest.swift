import Foundation

enum CameraDiagnosticsSelfTest {
  static func run() throws {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent("tufreplay-diagnostics-test-\(UUID().uuidString)")
    defer { try? FileManager.default.removeItem(at: directory) }
    let log = CameraDiagnostics(directory: directory, maxBytes: 1500, maxFiles: 2)
    let underlying = NSError(domain: "SyntheticCameraDevice", code: -23, userInfo: [NSLocalizedDescriptionKey: "Device disconnected"])
    let failure = NSError(domain: "SyntheticAVFoundation", code: -11819,
      userInfo: [NSLocalizedDescriptionKey: "Capture failed\nTry again", NSUnderlyingErrorKey: underlying])
    log.record("synthetic.failure", ["invalidTime": Double.nan], error: failure)
    log.flush()
    let first = try String(contentsOfFile: log.path, encoding: .utf8)
    let firstLines = first.split(separator: "\n")
    guard firstLines.count == 1,
      let entry = try JSONSerialization.jsonObject(with: Data(firstLines[0].utf8)) as? [String: Any],
      entry["event"] as? String == "synthetic.failure",
      let error = entry["error"] as? [String: Any], error["domain"] as? String == "SyntheticAVFoundation",
      error["code"] as? Int == -11819,
      let nested = error["underlyingError"] as? [String: Any], nested["domain"] as? String == "SyntheticCameraDevice",
      (entry["data"] as? [String: Any])?["invalidTime"] is NSNull else {
      throw CaptureError.message("Camera diagnostic logs lost native errors or produced invalid JSON lines.")
    }
    for index in 0..<60 { log.record("synthetic.health", ["index": index]) }
    log.flush()
    let files = try FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: [.fileSizeKey])
      .filter { $0.pathExtension == "jsonl" }
    guard files.count == 2 else { throw CaptureError.message("Camera diagnostic logs did not rotate within their file limit.") }
    for file in files {
      let bytes = try Data(contentsOf: file)
      guard bytes.count <= 1500, !bytes.isEmpty else {
        throw CaptureError.message("Camera diagnostic logs exceeded their size limit.")
      }
      for line in bytes.split(separator: 0x0A) { _ = try JSONSerialization.jsonObject(with: Data(line)) }
    }
    let permissions = try FileManager.default.attributesOfItem(atPath: log.path)[.posixPermissions] as? NSNumber
    guard permissions?.intValue == 0o600 else { throw CaptureError.message("Camera diagnostic log permissions were too broad.") }

    var statistics = CameraCaptureStatistics()
    statistics.lastHealthTime = 100
    for time in [100.0, 100.5, 101.0] { statistics.arrived(at: time) }
    statistics.encoded = 1
    guard statistics.skip("encoder-backpressure"), !statistics.skip("encoder-backpressure"),
      statistics.drop("FrameWasLate"), !statistics.drop("FrameWasLate") else {
      throw CaptureError.message("Camera diagnostic counters did not aggregate repeated failures.")
    }
    let state = statistics.snapshot(at: 101.5)
    guard state["receivedFrames"] as? Int64 == 3, state["receivedFpsSinceLastHealth"] as? Double == 2,
      state["lastFrameAgeSeconds"] as? Double == 0.5,
      statistics.skips["encoder-backpressure"] == 2, statistics.drops["FrameWasLate"] == 2 else {
      throw CaptureError.message("Camera diagnostic frame timing or counters were incorrect.")
    }
    let idle = WebcamCaptureService().diagnosticState()
    guard idle["phase"] as? String == "idle", idle["receivedFrames"] as? Int64 == 0,
      idle["lastFrameAgeSeconds"] is NSNull else {
      throw CaptureError.message("Camera diagnostic snapshots incorrectly reported frames before capture.")
    }
    let transport = DiagnosticTransport()
    let response = CameraEndResponse(deviceId: "synthetic", firstFrameHostTime: 9_007_199_254_740_993,
      durationUs: 400_000, sizeLimited: false, width: 640, height: 360)
    try transport.writeCamera(response, diagnostics: ["nativeError": CameraDiagnostics.errorDetails(failure)])
    struct Decoded: Decodable {
      let ok: Bool
      let firstFrameHostTime: UInt64
    }
    let decoded = try JSONDecoder().decode(Decoded.self, from: Data(transport.line.utf8))
    guard decoded.ok, decoded.firstFrameHostTime == response.firstFrameHostTime,
      let wrapped = try JSONSerialization.jsonObject(with: Data(transport.line.utf8)) as? [String: Any],
      let diagnostics = wrapped["diagnostics"] as? [String: Any], diagnostics["nativeError"] is [String: Any] else {
      throw CaptureError.message("Camera diagnostic responses changed capture timestamps or lost their context.")
    }
  }

  private final class DiagnosticTransport: JsonLineTransport {
    var line = ""
    func readLine() throws -> String? { nil }
    func writeLine(_ line: String) throws { self.line = line }
    func close() {}
  }
}
