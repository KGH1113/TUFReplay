import Foundation
import CoreMedia

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
    try verifyCaptureClock()

    let path = FileManager.default.temporaryDirectory
      .appendingPathComponent("tufreplay-microphone-self-test.wav")
    try audio.write(to: path, options: .atomic)
    defer { try? FileManager.default.removeItem(at: path) }
    return Result(bytes: audio.count)
  }

  private static func verifyCaptureClock() throws {
    let hostTicks: UInt64 = 9_007_199_254_740_993
    let response = CaptureEndResponse(frameCount: 480, deviceId: nil, firstSampleHostTime: hostTicks)
    let json = try JsonLineCodec.encode(response)
    struct TimestampResponse: Decodable { let firstSampleHostTime: UInt64 }
    let decoded = try JSONDecoder().decode(TimestampResponse.self, from: Data(json.utf8))
    guard decoded.firstSampleHostTime == hostTicks else {
      throw CaptureError.message("First-sample host timestamp lost precision in the end response.")
    }

    let hostClock = CMClockGetHostTimeClock()
    var timebase: CMTimebase?
    guard CMTimebaseCreateWithSourceClock(
      allocator: kCFAllocatorDefault, sourceClock: hostClock, timebaseOut: &timebase
    ) == noErr, let timebase else {
      throw CaptureError.message("Could not create the synthetic capture clock.")
    }
    let hostAnchor = CMClockGetTime(hostClock)
    // Different capture epochs and a delayed/trimmed first callback must map
    // to the same host timestamp. This also covers repeated capture sessions.
    for epoch in [7.0, 48.94, 90_000.0] {
      let captureAnchor = CMTime(seconds: epoch, preferredTimescale: 1_000_000)
      guard CMTimebaseSetRateAndAnchorTime(
        timebase, rate: 1, anchorTime: captureAnchor, immediateSourceTime: hostAnchor
      ) == noErr else {
        throw CaptureError.message("Could not set the synthetic capture epoch.")
      }
      guard let firstHost = CaptureBufferTiming.firstSampleHostTime(
        presentationTime: captureAnchor, skippedFrames: 480, sampleRate: 48_000, clock: timebase
      ) else {
        throw CaptureError.message("Capture sample timestamp conversion failed.")
      }
      let actual = CMClockMakeHostTimeFromSystemUnits(firstHost)
      let expected = CMTimeAdd(hostAnchor, CMTime(value: 1, timescale: 100))
      guard abs(CMTimeGetSeconds(CMTimeSubtract(actual, expected))) < 0.000_001 else {
        throw CaptureError.message("Capture clock epoch or trimmed samples shifted the host timestamp.")
      }
    }
    guard CaptureBufferTiming.firstSampleHostTime(
      presentationTime: .invalid, skippedFrames: 0, sampleRate: 48_000, clock: hostClock
    ) == nil else {
      throw CaptureError.message("An invalid sample timestamp was accepted.")
    }
  }
}
