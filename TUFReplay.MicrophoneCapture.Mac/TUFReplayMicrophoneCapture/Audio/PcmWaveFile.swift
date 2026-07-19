import Foundation

enum PcmWaveFile {
  static let sampleRate = 48_000
  static let channels = 1
  static let bitsPerSample = 16
  static let headerSize = 44

  static func header(frameCount: Int64) throws -> Data {
    let dataByteCount = try checkedDataByteCount(frameCount: frameCount)
    var data = Data()
    data.appendAscii("RIFF")
    data.appendLittleEndian(UInt32(36) + dataByteCount)
    data.appendAscii("WAVE")
    data.appendAscii("fmt ")
    data.appendLittleEndian(UInt32(16))
    data.appendLittleEndian(UInt16(1))
    data.appendLittleEndian(UInt16(channels))
    data.appendLittleEndian(UInt32(sampleRate))
    data.appendLittleEndian(UInt32(sampleRate * channels * bitsPerSample / 8))
    data.appendLittleEndian(UInt16(channels * bitsPerSample / 8))
    data.appendLittleEndian(UInt16(bitsPerSample))
    data.appendAscii("data")
    data.appendLittleEndian(dataByteCount)
    return data
  }

  private static func checkedDataByteCount(frameCount: Int64) throws -> UInt32 {
    guard frameCount >= 0 else { throw CaptureError.message("WAV frame count cannot be negative.") }
    let bytesPerFrame = Int64(channels * bitsPerSample / 8)
    let (byteCount, overflow) = frameCount.multipliedReportingOverflow(by: bytesPerFrame)
    guard !overflow, byteCount <= Int64(UInt32.max) - 36 else {
      throw CaptureError.message("WAV recording exceeds the RIFF size limit.")
    }
    return UInt32(byteCount)
  }
}

final class PcmWaveFileWriter {
  private let file: FileHandle
  private(set) var frameCount: Int64 = 0
  private var finished = false

  init(path: String) throws {
    let url = URL(fileURLWithPath: path)
    try FileManager.default.createDirectory(
      at: url.deletingLastPathComponent(),
      withIntermediateDirectories: true
    )
    guard
      FileManager.default.createFile(
        atPath: path,
        contents: try PcmWaveFile.header(frameCount: 0)
      )
    else {
      throw CaptureError.message("Could not create the microphone WAV file.")
    }
    file = try FileHandle(forWritingTo: url)
    try file.seekToEnd()
  }

  func append(pcm16 data: Data) throws {
    guard !finished else { throw CaptureError.message("Cannot append to a finalized WAV file.") }
    guard data.count % 2 == 0 else { throw CaptureError.message("PCM16 data length is invalid.") }
    try file.write(contentsOf: data)
    frameCount += Int64(data.count / 2)
  }

  @discardableResult
  func finish() throws -> Int64 {
    if finished { return frameCount }
    finished = true
    try file.seek(toOffset: 0)
    try file.write(contentsOf: PcmWaveFile.header(frameCount: frameCount))
    try file.synchronize()
    try file.close()
    return frameCount
  }
}

extension Data {
  fileprivate mutating func appendAscii(_ value: String) {
    append(value.data(using: .ascii)!)
  }

  fileprivate mutating func appendLittleEndian<T: FixedWidthInteger>(_ value: T) {
    var littleEndian = value.littleEndian
    Swift.withUnsafeBytes(of: &littleEndian) { append(contentsOf: $0) }
  }
}
