import Foundation

final class JsonLineCommandServer {
  private let service: MicrophoneCaptureService
  private let camera = WebcamCaptureService()
  private let transport: JsonLineTransport
  private let decoder = JSONDecoder()

  init(service: MicrophoneCaptureService, transport: JsonLineTransport) {
    self.service = service
    self.transport = transport
  }

  func run() {
    defer {
      service.disarm()
      camera.disarm()
      transport.close()

    }
    while true {
      let line: String
      do {
        guard let nextLine = try transport.readLine() else {

          return
        }
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
      try transport.write(AuthorizationResponse(authorizationStatus: try service.authorize()))
    case .authorizationStatus:
      try transport.write(AuthorizationResponse(authorizationStatus: try service.authorizationStatus()))
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
      camera.disarm()
      try transport.write(EmptyResponse())
      return false
    case .cameraDevices:
      try transport.write(CameraDevicesResponse(devices: camera.devices()))
    case .cameraArm:
      try camera.arm(
        deviceId: request.deviceId,
        profile: WebcamProfile(
          width: request.width ?? 640, height: request.height ?? 480,
          frameRate: request.frameRate ?? 30, bitRate: request.bitRate ?? 600_000
        ),
        previewPath: request.previewPath,
        recordingDirectory: request.recordingDirectory
      )
      try transport.write(EmptyResponse())
    case .cameraBegin:
      guard let path = request.path, let maxBytes = request.maxBytes else {
        throw CaptureError.message("Missing video path or storage limit.")
      }
      try camera.begin(path: path, maxBytes: maxBytes, startHostTime: request.startHostTime)
      try transport.write(EmptyResponse())
    case .cameraEnd:
      let result = try camera.end()
      try transport.write(result)
    case .cameraDisarm:
      camera.disarm()
      try transport.write(EmptyResponse())
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
