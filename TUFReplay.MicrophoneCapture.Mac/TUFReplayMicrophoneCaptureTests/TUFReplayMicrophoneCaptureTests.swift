import Testing
@testable import TUFReplayMicrophoneCapture

struct TUFReplayMicrophoneCaptureTests {
  @Test func syntheticWaveAndProtocolSelfTest() throws {
    let result = try MicrophoneCaptureSelfTest.run()

    #expect(result.bytes == PcmWaveFile.headerSize + 960)
  }
}
