import AppKit
import Foundation

final class ApplicationLauncher {
  func start(arguments: [String]) {
    guard !Self.isRunningUnitTests else { return }

    do {
      let options = try LaunchOptions.parse(arguments: arguments)
      let transport = try SocketJsonLineTransport(port: options.port)
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
