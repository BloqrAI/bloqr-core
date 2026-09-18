import Foundation

/// Errors surfaced by the Swift wrapper's configuration reading and compilation pipeline.
///
/// Mirrors the shape of `bloqr-compiler-core`'s (Rust) `CompilerError` enum so error messages
/// stay recognizable across wrappers, without attempting a one-to-one variant match.
public enum CompilerError: Error, LocalizedError, CustomStringConvertible {
    case configNotFound(path: String)
    case unknownExtension(extension: String)
    case parseFailed(format: String, underlying: String)
    case validationFailed(String)
    case invalidEngine(String)
    case compilerNotFound
    case processExecution(command: String, underlying: String)
    case fileSystem(context: String, underlying: String)
    case serialization(String)

    public var description: String {
        switch self {
        case .configNotFound(let path):
            return "configuration file not found: \(path)"
        case .unknownExtension(let ext):
            return "unrecognized configuration file extension: \(ext)"
        case .parseFailed(let format, let underlying):
            return "failed to parse \(format) configuration: \(underlying)"
        case .validationFailed(let message):
            return "configuration validation failed: \(message)"
        case .invalidEngine(let engine):
            return "invalid engine \"\(engine)\": expected \"dns\" or \"browser\""
        case .compilerNotFound:
            return "could not find \"deno\" on PATH - install Deno 2.0+ to run the compiler " +
                "(see https://deno.com/)"
        case .processExecution(let command, let underlying):
            return "failed to execute \"\(command)\": \(underlying)"
        case .fileSystem(let context, let underlying):
            return "file system error while \(context): \(underlying)"
        case .serialization(let message):
            return "failed to serialize configuration: \(message)"
        }
    }

    public var errorDescription: String? { description }
}
