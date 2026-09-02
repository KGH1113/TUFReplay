import Foundation

enum MicrophoneCaptureSelfTest {
  struct Result {
    let bytes: Int
  }

  static func run() throws -> Result {
    let frameCount: Int64 = 480
    var audio = try PcmWaveFile.header(frameCount: frameCount)
    audio.append(Data(repeating: 0, count: Int(frameCount) * 2))
    guard audio.count == PcmWaveFile.headerSize + 960 else {
      throw CaptureError.message("Synthetic WAV size validation failed.")
    }
    guard String(decoding: audio[0..<4], as: UTF8.self) == "RIFF" else {
      throw CaptureError.message("Synthetic WAV header validation failed.")
    }

    let command = try JSONDecoder().decode(
      CommandRequest.self,
      from: Data(#"{"command":"devices"}"#.utf8)
    )
    guard command.command == CommandName.devices.rawValue else {
      throw CaptureError.message("JSON command protocol validation failed.")
    }

    let authorizeCommand = try JSONDecoder().decode(
      CommandRequest.self,
      from: Data(#"{"command":"authorize"}"#.utf8)
    )
    guard authorizeCommand.command == CommandName.authorize.rawValue else {
      throw CaptureError.message("Authorization request command validation failed.")
    }

    let authorizationCommand = try JSONDecoder().decode(
      CommandRequest.self,
      from: Data(#"{"command":"authorizationStatus"}"#.utf8)
    )
    guard authorizationCommand.command == CommandName.authorizationStatus.rawValue else {
      throw CaptureError.message("Authorization status command validation failed.")
    }

    for status in MicrophoneAuthorizationStatus.allCases {
      let encoded = try JsonLineCodec.encode(AuthorizationResponse(authorizationStatus: status))
      guard
        let data = encoded.data(using: .utf8),
        let object = try JSONSerialization.jsonObject(with: data) as? [String: Any],
        object["ok"] as? Bool == true,
        object["authorizationStatus"] as? String == status.rawValue
      else {
        throw CaptureError.message("Authorization response encoding failed for \(status.rawValue).")
      }
    }

    try SocketTransportSelfTest.run()

    let path = FileManager.default.temporaryDirectory
      .appendingPathComponent("tufreplay-microphone-self-test.wav")
    try audio.write(to: path, options: .atomic)
    defer { try? FileManager.default.removeItem(at: path) }
    return Result(bytes: audio.count)
  }
}
