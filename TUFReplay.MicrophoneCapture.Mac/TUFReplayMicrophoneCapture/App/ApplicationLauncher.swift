import AppKit
import Foundation

final class ApplicationLauncher {
  func start(arguments: [String]) {
    guard !Self.isRunningUnitTests else { return }

    CameraDiagnostics.shared.record("helper.start", [
      "os": ProcessInfo.processInfo.operatingSystemVersionString,
      "bundlePath": Bundle.main.bundlePath,
      "bundleVersion": Bundle.main.infoDictionary?["CFBundleVersion"] as Any? ?? NSNull(),
      "executableModified": Bundle.main.executableURL.flatMap {
        try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate
      }.map { ISO8601DateFormatter().string(from: $0) } as Any? ?? NSNull(),
      "logPath": CameraDiagnostics.shared.path,
    ])
    do {
      let options = try LaunchOptions.parse(arguments: arguments)
      let transport = try SocketJsonLineTransport(port: options.port)
      CameraDiagnostics.shared.record("helper.connected", ["port": options.port])
      try transport.write(
        ConnectionHandshake(
          token: options.token,
          processId: Int32(ProcessInfo.processInfo.processIdentifier)
        )
      )

      let server = JsonLineCommandServer(
        service: MicrophoneCaptureService(),
        transport: transport
      )
      DispatchQueue.global(qos: .userInitiated).async {
        server.run()
        DispatchQueue.main.async {
          NSApplication.shared.terminate(nil)
        }
      }
    } catch {
      CameraDiagnostics.shared.record("helper.start.failed", error: error)
      CameraDiagnostics.shared.flush()
      fputs("TUFReplay microphone helper startup failed: \(error)\n", stderr)
      exit(1)
    }
  }

  private static var isRunningUnitTests: Bool {
    let environment = ProcessInfo.processInfo.environment
    return environment["XCTestConfigurationFilePath"] != nil
      || environment["XCInjectBundleInto"] != nil
  }
}
