import Foundation

struct LaunchOptions {
  let port: UInt16
  let token: String

  static func parse(arguments: [String]) throws -> LaunchOptions {
    guard
      let portText = value(after: "--connect-port", in: arguments),
      let port = UInt16(portText),
      port > 0
    else {
      throw CaptureError.message("Missing or invalid --connect-port.")
    }
    guard let token = value(after: "--token", in: arguments), !token.isEmpty else {
      throw CaptureError.message("Missing --token.")
    }
    return LaunchOptions(port: port, token: token)
  }

  private static func value(after option: String, in arguments: [String]) -> String? {
    guard let index = arguments.firstIndex(of: option) else { return nil }
    let valueIndex = arguments.index(after: index)
    return valueIndex < arguments.endIndex ? arguments[valueIndex] : nil
  }
}
