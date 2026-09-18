import Foundation

/// Result of running an external process to completion.
struct ProcessOutput {
    var exitCode: Int32
    var stdout: String
    var stderr: String
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

    let stdoutData = stdoutPipe.fileHandleForReading.readDataToEndOfFile()
    let stderrData = stderrPipe.fileHandleForReading.readDataToEndOfFile()
    process.waitUntilExit()

    return ProcessOutput(
        exitCode: process.terminationStatus,
        stdout: String(data: stdoutData, encoding: .utf8) ?? "",
        stderr: String(data: stderrData, encoding: .utf8) ?? ""
    )
}
