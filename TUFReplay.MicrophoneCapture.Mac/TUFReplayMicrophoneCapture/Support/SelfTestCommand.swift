import Foundation

enum SelfTestCommand {
  static func runAndExit() -> Never {
    do {
      let result = try MicrophoneCaptureSelfTest.run()
      JsonLineCodec.writeToStandardOutput(SelfTestResponse(bytes: result.bytes))
      exit(0)
    } catch {
      JsonLineCodec.writeToStandardOutput(ErrorResponse(error: String(describing: error)))
      exit(1)
    }
  }
}
