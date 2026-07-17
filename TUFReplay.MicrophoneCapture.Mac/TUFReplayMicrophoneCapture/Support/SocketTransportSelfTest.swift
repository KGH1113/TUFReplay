import Darwin
import Foundation

enum SocketTransportSelfTest {
  static func run() throws {
    var descriptors = [Int32](repeating: -1, count: 2)
    guard Darwin.socketpair(AF_UNIX, SOCK_STREAM, 0, &descriptors) == 0 else {
      throw CaptureError.posix("Socket transport self-test could not create a socket pair.")
    }

    let peerDescriptor = descriptors[1]
    var timeout = timeval(tv_sec: 1, tv_usec: 0)
    guard
      setsockopt(
        descriptors[0],
        SOL_SOCKET,
        SO_RCVTIMEO,
        &timeout,
        socklen_t(MemoryLayout<timeval>.size)
      ) == 0
    else {
      Darwin.close(descriptors[0])
      Darwin.close(peerDescriptor)
      throw CaptureError.posix("Socket transport self-test could not set a receive timeout.")
    }

    let transport: SocketJsonLineTransport
    do {
      transport = try SocketJsonLineTransport(connectedDescriptor: descriptors[0])
    } catch {
      Darwin.close(descriptors[0])
      Darwin.close(peerDescriptor)
      throw error
    }
    defer {
      transport.close()
      Darwin.close(peerDescriptor)
    }

    let payload = Data("{\"command\":\"devices\"}\n".utf8)
    let sent = payload.withUnsafeBytes { bytes in
      Darwin.send(peerDescriptor, bytes.baseAddress, bytes.count, 0)
    }
    guard sent == payload.count else {
      throw CaptureError.posix("Socket transport self-test could not write its command.")
    }
    guard try transport.readLine() == #"{"command":"devices"}"# else {
      throw CaptureError.message("Socket transport waited for EOF instead of returning one line.")
    }
  }
}
