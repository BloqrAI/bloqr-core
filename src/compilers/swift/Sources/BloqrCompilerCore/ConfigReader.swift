import Foundation
import Yams
import TOMLKit

/// Reads and serializes `CompilerConfig` values across JSON/YAML/TOML, mirroring the other
/// four wrappers' `read_config()`/`to_json()`/`to_yaml()`/`to_toml()` functions.
public enum ConfigReader {
    /// Reads configuration from a file. When `format` is `nil`, it is detected from the file
    /// extension.
    public static func readConfig(path: URL, format: ConfigFormat? = nil) throws -> CompilerConfig {
        guard FileManager.default.fileExists(atPath: path.path) else {
            throw CompilerError.configNotFound(path: path.path)
        }

        let resolvedFormat = try format ?? (try? ConfigFormat.from(path: path)) ?? .json
        let content: String
        do {
            content = try String(contentsOf: path, encoding: .utf8)
        } catch {
            throw CompilerError.fileSystem(
                context: "reading configuration from \(path.path)",
                underlying: error.localizedDescription
            )
        }

        var config = try parse(content, format: resolvedFormat)
        config.sourceFormat = resolvedFormat
        config.sourcePath = path
        return config
    }

    static func parse(_ content: String, format: ConfigFormat) throws -> CompilerConfig {
        switch format {
        case .json:
            do {
                let data = Data(content.utf8)
                return try JSONDecoder().decode(CompilerConfig.self, from: data)
            } catch {
                throw CompilerError.parseFailed(format: "JSON", underlying: String(describing: error))
            }
        case .yaml:
            do {
                let decoder = YAMLDecoder()
                return try decoder.decode(CompilerConfig.self, from: content)
            } catch {
                throw CompilerError.parseFailed(format: "YAML", underlying: String(describing: error))
            }
        case .toml:
            do {
                return try TOMLDecoder().decode(CompilerConfig.self, from: content)
            } catch {
                throw CompilerError.parseFailed(format: "TOML", underlying: String(describing: error))
            }
        }
    }

    /// Serializes a configuration back to a pretty-printed JSON string.
    public static func toJSON(_ config: CompilerConfig) throws -> String {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        do {
            let data = try encoder.encode(config)
            guard let json = String(data: data, encoding: .utf8) else {
                throw CompilerError.serialization("could not decode encoded JSON as UTF-8")
            }
            return json
        } catch let error as CompilerError {
            throw error
        } catch {
            throw CompilerError.serialization(String(describing: error))
        }
    }

    /// Serializes a configuration back to a YAML string.
    public static func toYAML(_ config: CompilerConfig) throws -> String {
        // Round-trip through JSON so key ordering/coding-key rules match the JSON encoder,
        // rather than depending on YAMLEncoder's own Codable handling of optionals.
        let json = try toJSON(config)
        guard let jsonData = json.data(using: .utf8),
              let object = try JSONSerialization.jsonObject(with: jsonData) as? [String: Any] else {
            throw CompilerError.serialization("could not round-trip configuration through JSON for YAML output")
        }
        return try Yams.dump(object: object)
    }

    /// Serializes a configuration back to a TOML string.
    public static func toTOML(_ config: CompilerConfig) throws -> String {
        do {
            return try TOMLEncoder().encode(config)
        } catch {
            throw CompilerError.serialization(String(describing: error))
        }
    }
}
