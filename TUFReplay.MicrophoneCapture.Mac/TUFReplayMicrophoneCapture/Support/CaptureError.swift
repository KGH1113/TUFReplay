import Darwin

enum CaptureError: Error, CustomStringConvertible {
  case message(String)

  static func posix(_ message: String) -> CaptureError {
    .message("\(message) errno=\(errno) (\(String(cString: strerror(errno))))")
  }

  var description: String {
    switch self {
    case .message(let value):
      return value
    }
  }
}
