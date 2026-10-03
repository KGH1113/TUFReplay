import Foundation

struct CommandRequest: Decodable {
  let command: String
  let deviceId: String?
  let runId: String?
  let path: String?
  let width: Int?
  let height: Int?
  let frameRate: Int?
  let bitRate: Int?
  let maxBytes: Int64?
  let previewPath: String?
}

enum CommandName: String {
  case devices
  case authorize
  case authorizationStatus
  case arm
  case begin
  case end
  case disarm
  case shutdown
  case cameraDevices
  case cameraArm
  case cameraBegin
  case cameraEnd
  case cameraDisarm
  case cameraDiagnostics
}

struct ConnectionHandshake: Encodable {
  let token: String
  let processId: Int32
  let protocolVersion = 3
  let diagnosticsLogPath = CameraDiagnostics.shared.path
}

enum MicrophoneAuthorizationStatus: String, CaseIterable, Encodable {
  case notDetermined
  case authorized
  case denied
  case restricted
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

struct AuthorizationResponse: Encodable {
  let ok = true
  let authorizationStatus: MicrophoneAuthorizationStatus
}

struct CaptureEndResponse: Encodable {
  let ok = true
  let frameCount: Int64
  let sampleRate = PcmWaveFile.sampleRate
  let channels = PcmWaveFile.channels
  let deviceId: String?
  let firstSampleHostTime: UInt64
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

struct CameraDeviceResponse: Encodable {
  let id: String
  let name: String
}

struct CameraDevicesResponse: Encodable {
  let ok = true
  let devices: [CameraDeviceResponse]
}

struct CameraEndResponse: Encodable {
  let ok = true
  let deviceId: String?
  let firstFrameHostTime: UInt64
  let durationUs: Int64
  let sizeLimited: Bool
  let width: Int
  let height: Int
}
