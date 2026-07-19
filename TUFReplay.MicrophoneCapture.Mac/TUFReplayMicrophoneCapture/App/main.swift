import AppKit

if CommandLine.arguments.contains("--self-test") {
  SelfTestCommand.runAndExit()
}

let application = NSApplication.shared
let delegate = AppDelegate(arguments: CommandLine.arguments)
application.delegate = delegate
application.setActivationPolicy(.accessory)
application.run()
