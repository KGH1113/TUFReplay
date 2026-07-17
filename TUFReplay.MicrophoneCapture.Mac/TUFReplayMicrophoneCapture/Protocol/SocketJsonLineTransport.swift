import Darwin
import Foundation

final class SocketJsonLineTransport: JsonLineTransport {
  private var descriptor: Int32
  private var readBuffer = Data()
  private var receiveBuffer = [UInt8](repeating: 0, count: 4096)

  convenience init(port: UInt16) throws {
    let descriptor = Darwin.socket(AF_INET, SOCK_STREAM, 0)
    guard descriptor >= 0 else { throw CaptureError.posix("Could not create loopback socket.") }

    var address = sockaddr_in()
    address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
    address.sin_family = sa_family_t(AF_INET)
    address.sin_port = port.bigEndian
    guard inet_pton(AF_INET, "127.0.0.1", &address.sin_addr) == 1 else {
      Darwin.close(descriptor)
      throw CaptureError.message("Could not resolve the loopback address.")
    }

    let result = withUnsafePointer(to: &address) { pointer in
      pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
        Darwin.connect(descriptor, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
      }
    }
    guard result == 0 else {
      Darwin.close(descriptor)
      throw CaptureError.posix("Could not connect to the TUFReplay host.")
    }

    do {
      try self.init(connectedDescriptor: descriptor)
    } catch {
      Darwin.close(descriptor)
      throw error
    }
  }

  init(connectedDescriptor: Int32) throws {
    descriptor = connectedDescriptor
    var noSigPipe: Int32 = 1
    guard
      setsockopt(
        descriptor,
        SOL_SOCKET,
        SO_NOSIGPIPE,
        &noSigPipe,
        socklen_t(MemoryLayout<Int32>.size)
      ) == 0
    else {
      throw CaptureError.posix("Could not configure the loopback socket.")
    }
  }

  deinit {
    close()
  }

  func readLine() throws -> String? {
    while true {
      if let newline = readBuffer.firstIndex(of: 0x0A) {
        let line = readBuffer[..<newline]
        readBuffer.removeSubrange(...newline)
        guard let value = String(data: line, encoding: .utf8) else {
          throw CaptureError.message("Host sent invalid UTF-8.")
        }
        return value
      }

      let received = receiveBuffer.withUnsafeMutableBytes { bytes in
        Darwin.recv(descriptor, bytes.baseAddress, bytes.count, 0)
      }
      if received > 0 {
        readBuffer.append(contentsOf: receiveBuffer.prefix(received))
        continue
      }
      if received < 0, errno == EINTR { continue }
      if received < 0 { throw CaptureError.posix("Could not read from the TUFReplay host.") }

      if readBuffer.isEmpty { return nil }
      defer { readBuffer.removeAll(keepingCapacity: true) }
      guard let value = String(data: readBuffer, encoding: .utf8) else {
        throw CaptureError.message("Host sent invalid UTF-8.")
      }
      return value
    }
  }

  func writeLine(_ line: String) throws {
    guard descriptor >= 0 else { throw CaptureError.message("Host connection is closed.") }
    var data = Data(line.utf8)
    data.append(0x0A)

    try data.withUnsafeBytes { bytes in
      guard let baseAddress = bytes.baseAddress else { return }
      var offset = 0
      while offset < bytes.count {
        let sent = Darwin.send(
          descriptor,
          baseAddress.advanced(by: offset),
          bytes.count - offset,
          0
        )
        if sent > 0 {
          offset += sent
        } else if sent < 0, errno == EINTR {
          continue
        } else {
          throw CaptureError.posix("Could not write to the TUFReplay host.")
        }
      }
    }
  }

  func close() {
    guard descriptor >= 0 else { return }
    let openDescriptor = descriptor
    descriptor = -1
    Darwin.shutdown(openDescriptor, SHUT_RDWR)
    Darwin.close(openDescriptor)
  }
}
