import Foundation

struct CommandRequest: Decodable {
  let command: String
  let deviceId: String?
  let runId: String?
  let path: String?
}

enum CommandName: String {
  case devices
  case authorize
  case arm
  case begin
  case end
  case disarm
  case shutdown
}

struct ConnectionHandshake: Encodable {
  let token: String
  let processId: Int32
  let protocolVersion = 1
}

struct MicrophoneDeviceResponse: Encodable {
  let id: String
  let name: String
  let minFrequency = PcmWaveFile.sampleRate
  let maxFrequency = PcmWaveFile.sampleRate
}

struct EmptyResponse: Encodable {
  let ok = true
}

struct DevicesResponse: Encodable {
  let ok = true
  let devices: [MicrophoneDeviceResponse]
}

struct CaptureEndResponse: Encodable {
  let ok = true
  let frameCount: Int64
  let sampleRate = PcmWaveFile.sampleRate
  let channels = PcmWaveFile.channels
  let deviceId: String?
  let captureStartOffsetUs: Int64
}

struct SelfTestResponse: Encodable {
  let ok = true
  let selfTest = true
  let bytes: Int
}

struct ErrorResponse: Encodable {
  let ok = false
  let error: String
}
