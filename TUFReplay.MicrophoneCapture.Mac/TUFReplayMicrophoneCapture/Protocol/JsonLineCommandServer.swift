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
      CameraDiagnostics.shared.record("helper.transport.closed")
      CameraDiagnostics.shared.flush()
    }
    while true {
      let line: String
      do {
        guard let nextLine = try transport.readLine() else {
          CameraDiagnostics.shared.record("helper.transport.eof")
          return
        }
        line = nextLine
      } catch {
        CameraDiagnostics.shared.record("helper.transport.read.failed", error: error)
        return
      }

      var currentCommand: String?
      let startedAt = ProcessInfo.processInfo.systemUptime
      do {
        guard let data = line.data(using: .utf8) else {
          throw CaptureError.message("Invalid UTF-8 command.")
        }
        let request = try decoder.decode(CommandRequest.self, from: data)
        currentCommand = request.command
        if request.command.hasPrefix("camera") {
          CameraDiagnostics.shared.record("command.begin", [
            "command": request.command, "deviceId": request.deviceId as Any? ?? NSNull(),
            "runId": request.runId as Any? ?? NSNull(), "path": request.path as Any? ?? NSNull(),
            "previewPath": request.previewPath as Any? ?? NSNull(),
            "recordingDirectory": request.recordingDirectory as Any? ?? NSNull(),
            "width": request.width as Any? ?? NSNull(), "height": request.height as Any? ?? NSNull(),
            "frameRate": request.frameRate as Any? ?? NSNull(), "bitRate": request.bitRate as Any? ?? NSNull(),
            "maxBytes": request.maxBytes as Any? ?? NSNull(),
          ])
        }
        if try !handle(request) { return }
        if request.command.hasPrefix("camera") {
          CameraDiagnostics.shared.record("command.complete", [
            "command": request.command, "elapsedMs": (ProcessInfo.processInfo.systemUptime - startedAt) * 1000,
          ])
        }
      } catch {
        var state = camera.diagnosticState()
        state["nativeError"] = CameraDiagnostics.errorDetails(error)
        state["command"] = currentCommand as Any? ?? NSNull()
        state["elapsedMs"] = (ProcessInfo.processInfo.systemUptime - startedAt) * 1000
        CameraDiagnostics.shared.record("command.failed", state, error: error)
        if currentCommand?.hasPrefix("camera") == true {
          try? transport.writeCamera(ErrorResponse(error: String(describing: error)), diagnostics: state)
        } else {
          try? transport.write(ErrorResponse(error: String(describing: error)))
        }
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
      try transport.writeCamera(CameraDevicesResponse(devices: camera.devices()), diagnostics: camera.diagnosticState())
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
      try transport.writeCamera(EmptyResponse(), diagnostics: camera.diagnosticState())
    case .cameraBegin:
      guard let path = request.path, let maxBytes = request.maxBytes else {
        throw CaptureError.message("Missing video path or storage limit.")
      }
      try camera.begin(path: path, maxBytes: maxBytes, runId: request.runId, startHostTime: request.startHostTime)
      try transport.writeCamera(EmptyResponse(), diagnostics: camera.diagnosticState())
    case .cameraEnd:
      let result = try camera.end()
      try transport.writeCamera(result, diagnostics: camera.diagnosticState())
    case .cameraDisarm:
      camera.disarm()
      try transport.writeCamera(EmptyResponse(), diagnostics: camera.diagnosticState())
    case .cameraDiagnostics:
      try transport.writeCamera(EmptyResponse(), diagnostics: camera.diagnosticState())
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

  func writeCamera<T: Encodable>(_ value: T, diagnostics: [String: Any]) throws {
    var payload = try JSONSerialization.jsonObject(with: Data(JsonLineCodec.encode(value).utf8)) as? [String: Any] ?? [:]
    payload["diagnostics"] = CameraDiagnostics.safe(diagnostics)
    try writeLine(String(decoding: JSONSerialization.data(withJSONObject: payload, options: [.sortedKeys]), as: UTF8.self))
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
