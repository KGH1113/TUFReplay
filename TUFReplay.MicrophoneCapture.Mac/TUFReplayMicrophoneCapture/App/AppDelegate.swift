import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
  private let launcher = ApplicationLauncher()
  private let arguments: [String]

  init(arguments: [String]) {
    self.arguments = arguments
  }

  func applicationDidFinishLaunching(_ notification: Notification) {
    launcher.start(arguments: arguments)
  }

  func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
    true
  }
}
