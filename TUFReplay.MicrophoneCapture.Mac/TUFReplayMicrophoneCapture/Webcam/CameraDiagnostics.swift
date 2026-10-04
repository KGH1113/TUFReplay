import Foundation

// Capture callbacks enqueue small events. JSON encoding, log rotation, and disk
// writes run on a separate queue, never on the frame or preview worker.
final class CameraDiagnostics {
  static let shared = CameraDiagnostics()
  private let queue = DispatchQueue(label: "impl.tufreplay.camera.diagnostics", qos: .utility)
  private let directory: URL
  private let url: URL
  private let previous: URL
  private let maxBytes: Int
  private let maxFiles: Int
  private let formatter = ISO8601DateFormatter()
  private var handle: FileHandle?
  private var bytes = 0
  private var reportedWriteFailure = false
  var path: String { url.path }

  init(directory: URL = FileManager.default.homeDirectoryForCurrentUser
    .appendingPathComponent("Library/Logs/TUFReplay", isDirectory: true),
    maxBytes: Int = 2 * 1024 * 1024, maxFiles: Int = 12) {
    self.directory = directory
    self.maxBytes = maxBytes
    self.maxFiles = maxFiles
    let stamp = ISO8601DateFormatter().string(from: Date()).replacingOccurrences(of: ":", with: "-")
    let name = "camera-helper-\(stamp)-\(ProcessInfo.processInfo.processIdentifier)-\(UUID().uuidString.prefix(8))"
    url = directory.appendingPathComponent(name + ".jsonl")
    previous = directory.appendingPathComponent(name + ".previous.jsonl")
    formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
  }

  func record(_ event: String, _ fields: [String: Any] = [:], error: Error? = nil) {
    let date = Date()
    let uptime = ProcessInfo.processInfo.systemUptime
    queue.async {
      var record: [String: Any] = [
        "utc": self.formatter.string(from: date), "uptimeSeconds": uptime,
        "processId": ProcessInfo.processInfo.processIdentifier, "event": event,
        "selfTest": ProcessInfo.processInfo.arguments.contains("--self-test"),
        "data": Self.safe(fields),
      ]
      if let error { record["error"] = Self.errorDetails(error) }
      self.append(record)
    }
  }

  func flush() { queue.sync {} }

  private func append(_ record: [String: Any]) {
    do {
      var line = try JSONSerialization.data(withJSONObject: record, options: [.sortedKeys])
      line.append(0x0A)
      if handle == nil {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
          attributes: [.posixPermissions: 0o700])
        guard FileManager.default.createFile(atPath: url.path, contents: nil, attributes: [.posixPermissions: 0o600]) else {
          throw CaptureError.message("Could not create the camera diagnostic log.")
        }
        handle = try FileHandle(forWritingTo: url)
        prune()
      }
      if bytes > 0 && bytes + line.count > maxBytes {
        try handle?.close()
        handle = nil
        try? FileManager.default.removeItem(at: previous)
        try FileManager.default.moveItem(at: url, to: previous)
        guard FileManager.default.createFile(atPath: url.path, contents: nil, attributes: [.posixPermissions: 0o600]) else {
          throw CaptureError.message("Could not rotate the camera diagnostic log.")
        }
        handle = try FileHandle(forWritingTo: url)
        bytes = 0
        prune()
      }
      try handle?.write(contentsOf: line)
      bytes += line.count
    } catch {
      if !reportedWriteFailure {
        reportedWriteFailure = true
        fputs("TUFReplay camera diagnostic log failed: \(error) path=\(path)\n", stderr)
      }
    }
  }

  private func prune() {
    let files = (try? FileManager.default.contentsOfDirectory(at: directory,
      includingPropertiesForKeys: [.contentModificationDateKey])) ?? []
    let logs = files.filter { $0.lastPathComponent.hasPrefix("camera-helper-") && $0.pathExtension == "jsonl" }
      .sorted {
        let a = (try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
        let b = (try? $1.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
        return a > b
      }
    var remaining = logs.count - maxFiles
    for file in logs.reversed() where remaining > 0 && file != url && file != previous {
      try? FileManager.default.removeItem(at: file)
      remaining -= 1
    }
  }

  static func errorDetails(_ error: Error, depth: Int = 0) -> [String: Any] {
    let native = error as NSError
    var result: [String: Any] = [
      "type": String(reflecting: type(of: error)), "domain": native.domain, "code": native.code,
      "description": String(describing: error), "localizedDescription": native.localizedDescription,
    ]
    if depth < 4 {
      result["userInfo"] = safe(native.userInfo, depth: depth + 1)
      if let underlying = native.userInfo[NSUnderlyingErrorKey] as? NSError {
        result["underlyingError"] = errorDetails(underlying, depth: depth + 1)
      }
    }
    return result
  }

  static func safe(_ value: Any, depth: Int = 0) -> Any {
    if depth > 5 { return String(describing: value).prefix(4096).description }
    switch value {
    case let error as NSError: return errorDetails(error, depth: depth)
    case let fields as [String: Any]:
      return Dictionary(uniqueKeysWithValues: fields.sorted { $0.key < $1.key }.prefix(64)
        .map { ($0.key, safe($0.value, depth: depth + 1)) })
    case let values as [Any]: return values.prefix(64).map { safe($0, depth: depth + 1) }
    case let number as NSNumber:
      return number.doubleValue.isFinite ? number : NSNull()
    case let string as String: return String(string.prefix(4096))
    case is NSNull: return NSNull()
    default: return String(String(describing: value).prefix(4096))
    }
  }
}
