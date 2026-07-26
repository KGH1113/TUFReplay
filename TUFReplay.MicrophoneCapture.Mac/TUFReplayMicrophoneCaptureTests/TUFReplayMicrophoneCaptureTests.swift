import Testing
import CoreMedia
@testable import TUFReplayMicrophoneCapture

struct TUFReplayMicrophoneCaptureTests {
  @Test func syntheticWaveAndProtocolSelfTest() throws {
    let result = try MicrophoneCaptureSelfTest.run()

    #expect(result.bytes == PcmWaveFile.headerSize + 960)
  }

  @Test func trimsSamplesCapturedBeforeBegin() {
    let slice = CaptureBufferTiming.firstWritableSlice(
      presentationTime: CMTime(seconds: 9.99, preferredTimescale: 1_000_000),
      beginTime: CMTime(seconds: 10, preferredTimescale: 1_000_000),
      sampleRate: 48_000,
      sampleCount: 960
    )

    #expect(slice?.skippedFrames == 480)
    #expect(slice?.startOffsetUs == 0)
  }

  @Test func preservesFirstSamplePresentationOffset() {
    let slice = CaptureBufferTiming.firstWritableSlice(
      presentationTime: CMTime(seconds: 10.005, preferredTimescale: 1_000_000),
      beginTime: CMTime(seconds: 10, preferredTimescale: 1_000_000),
      sampleRate: 48_000,
      sampleCount: 960
    )

    #expect(slice?.skippedFrames == 0)
    #expect(slice?.startOffsetUs == 5_000)
  }

  @Test func rejectsBuffersEntirelyBeforeBegin() {
    let slice = CaptureBufferTiming.firstWritableSlice(
      presentationTime: CMTime(seconds: 9.97, preferredTimescale: 1_000_000),
      beginTime: CMTime(seconds: 10, preferredTimescale: 1_000_000),
      sampleRate: 48_000,
      sampleCount: 960
    )

    #expect(slice == nil)
  }
}
