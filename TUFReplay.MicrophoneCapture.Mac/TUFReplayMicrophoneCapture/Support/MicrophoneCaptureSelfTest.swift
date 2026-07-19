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

    try SocketTransportSelfTest.run()

    let path = FileManager.default.temporaryDirectory
      .appendingPathComponent("tufreplay-microphone-self-test.wav")
    try audio.write(to: path, options: .atomic)
    defer { try? FileManager.default.removeItem(at: path) }
    return Result(bytes: audio.count)
  }
}
