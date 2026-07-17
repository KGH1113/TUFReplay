import Foundation

final class JsonLineCommandServer {
  private let service: MicrophoneCaptureService
  private let transport: JsonLineTransport
  private let decoder = JSONDecoder()

  init(service: MicrophoneCaptureService, transport: JsonLineTransport) {
    self.service = service
    self.transport = transport
  }

  func run() {
    defer {
      service.disarm()
      transport.close()
    }
    while true {
      let line: String
      do {
        guard let nextLine = try transport.readLine() else { return }
        line = nextLine
      } catch {
        return
      }

      do {
        guard let data = line.data(using: .utf8) else {
          throw CaptureError.message("Invalid UTF-8 command.")
        }
        let request = try decoder.decode(CommandRequest.self, from: data)
        if try !handle(request) { return }
      } catch {
        try? transport.write(ErrorResponse(error: String(describing: error)))
      }
    }
  }

  private func handle(_ request: CommandRequest) throws -> Bool {
    guard let command = CommandName(rawValue: request.command) else {
      throw CaptureError.message("Unknown command: \(request.command)")
    }
    switch command {
    case .devices:
      try transport.write(DevicesResponse(devices: service.devices()))
    case .authorize:
      try service.authorize()
      try transport.write(EmptyResponse())
    case .arm:
      try service.arm(deviceId: request.deviceId)
      try transport.write(EmptyResponse())
    case .begin:
      guard let path = request.path else { throw CaptureError.message("Missing path.") }
      try service.begin(path: path)
      try transport.write(EmptyResponse())
    case .end:
      try transport.write(service.end())
    case .disarm:
      service.disarm()
      try transport.write(EmptyResponse())
    case .shutdown:
      service.disarm()
      try transport.write(EmptyResponse())
      return false
    }
    return true
  }
}

protocol JsonLineTransport: AnyObject {
  func readLine() throws -> String?
  func writeLine(_ line: String) throws
  func close()
}

extension JsonLineTransport {
  func write<T: Encodable>(_ value: T) throws {
    try writeLine(JsonLineCodec.encode(value))
  }
}

enum JsonLineCodec {
  private static let encoder = JSONEncoder()

  static func encode<T: Encodable>(_ value: T) throws -> String {
    String(decoding: try encoder.encode(value), as: UTF8.self)
  }

  static func writeToStandardOutput<T: Encodable>(_ value: T) {
    do {
      print(try encode(value))
      fflush(stdout)
    } catch {
      fputs("JSON encoding failed: \(error)\n", stderr)
    }
  }
}
