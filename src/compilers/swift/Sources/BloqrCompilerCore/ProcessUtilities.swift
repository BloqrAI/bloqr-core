import Foundation

/// Result of running an external process to completion.
struct ProcessOutput {
    var exitCode: Int32
    var stdout: String
    var stderr: String
}

/// Mutable box handed to a background queue so its captured `Data` can be read back after
/// `DispatchGroup.wait()` without a `Sendable` warning on a plain `var` capture.
private final class ReadResultBox: @unchecked Sendable {
    var data = Data()
}

/// Locates an executable on `PATH`, mirroring the Rust wrapper's `which::which()` use.
func findCommand(_ name: String) -> String? {
    guard let pathVar = ProcessInfo.processInfo.environment["PATH"] else { return nil }
    let fileManager = FileManager.default
    for directory in pathVar.split(separator: ":") {
        let candidate = "\(directory)/\(name)"
        if fileManager.isExecutableFile(atPath: candidate) {
            return candidate
        }
    }
    return nil
}

/// Runs `command` with `arguments` and returns its first line of stdout, or `nil` if the
/// command couldn't be run or exited non-zero.
func commandOutput(_ command: String, _ arguments: [String]) -> String? {
    guard let output = try? runProcess(command: command, arguments: arguments, currentDirectory: nil),
          output.exitCode == 0 else {
        return nil
    }
    return output.stdout
}

/// Runs `command` with `arguments`, waiting for it to exit, and captures stdout/stderr.
func runProcess(command: String, arguments: [String], currentDirectory: URL?) throws -> ProcessOutput {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: command)
    process.arguments = arguments
    if let currentDirectory {
        process.currentDirectoryURL = currentDirectory
    }

    let stdoutPipe = Pipe()
    let stderrPipe = Pipe()
    process.standardOutput = stdoutPipe
    process.standardError = stderrPipe

    do {
        try process.run()
    } catch {
        throw CompilerError.processExecution(
            command: ([command] + arguments).joined(separator: " "),
            underlying: error.localizedDescription
        )
    }

    // Drain both pipes concurrently, not sequentially: if the child fills the stderr pipe's
    // buffer while still writing stdout (or vice versa), reading one pipe to EOF before
    // touching the other deadlocks - the child blocks on the full pipe while this process
    // blocks waiting for the other pipe's EOF.
    let stdoutBox = ReadResultBox()
    let stderrBox = ReadResultBox()
    let group = DispatchGroup()

    group.enter()
    DispatchQueue.global(qos: .utility).async {
        stdoutBox.data = stdoutPipe.fileHandleForReading.readDataToEndOfFile()
        group.leave()
    }
    group.enter()
    DispatchQueue.global(qos: .utility).async {
        stderrBox.data = stderrPipe.fileHandleForReading.readDataToEndOfFile()
        group.leave()
    }
    group.wait()

    process.waitUntilExit()
    let stdoutData = stdoutBox.data
    let stderrData = stderrBox.data

    return ProcessOutput(
        exitCode: process.terminationStatus,
        stdout: String(data: stdoutData, encoding: .utf8) ?? "",
        stderr: String(data: stderrData, encoding: .utf8) ?? ""
    )
}
