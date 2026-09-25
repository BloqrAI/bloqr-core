import Foundation
import Yams
import TOMLKit

/// Reads and serializes `CompilerConfig` values across JSON/YAML/TOML, mirroring the other
/// four wrappers' `read_config()`/`to_json()`/`to_yaml()`/`to_toml()` functions.
public enum ConfigReader {
    /// Reads configuration from a file. When `format` is `nil`, it is detected from the file
    /// extension.
    public static func readConfig(path: URL, format: ConfigFormat? = nil) throws(CompilerError) -> CompilerConfig {
        guard FileManager.default.fileExists(atPath: path.path) else {
            throw CompilerError.configNotFound(path: path.path)
        }

        let resolvedFormat = format ?? (try? ConfigFormat.from(path: path)) ?? .json
        let content: String
        do {
            content = try String(contentsOf: path, encoding: .utf8)
        } catch {
            throw CompilerError.fileSystem(
                context: "reading configuration from \(path.path)",
                underlying: error.localizedDescription
            )
        }

        if let rawValue = try parseRawValue(content, format: resolvedFormat) {
            try SchemaValidation.assertValid(rawValue: rawValue)
        }

        var config = try parse(content, format: resolvedFormat)
        try SchemaValidation.assertValid(config)
        config.sourceFormat = resolvedFormat
        config.sourcePath = path
        return config
    }

    /// Parses `content` into a JSON-compatible `[String: Any]`/array/scalar tree - the raw
    /// document, before `CompilerConfig`'s `Decodable` conformance can silently drop any key it
    /// doesn't model - so `SchemaValidation` can validate every key actually present in the
    /// file, not just the subset `CompilerConfig` re-encodes.
    ///
    /// JSON only for now: `JSONSerialization` gives an exact, order-independent bridge from the
    /// stripped-of-comments source text. YAML/TOML remain functionally supported but
    /// undocumented (see CLAUDE.md), and unlike JSON there's no already-vetted bridge from
    /// Yams/TOMLKit's own tree types to `Any` in this codebase - `nil` here just means those two
    /// formats fall back to the existing model-level `SchemaValidation.assertValid(_:)` check
    /// after decoding, same as before this change.
    static func parseRawValue(_ content: String, format: ConfigFormat) throws(CompilerError) -> Any? {
        guard format == .json else { return nil }
        let stripped = try stripJSONCComments(content)
        guard let data = stripped.data(using: .utf8) else { return nil }
        return try? JSONSerialization.jsonObject(with: data)
    }

    static func parse(_ content: String, format: ConfigFormat) throws(CompilerError) -> CompilerConfig {
        switch format {
        case .json:
            do {
                let data = Data(try stripJSONCComments(content).utf8)
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
    public static func toJSON(_ config: CompilerConfig) throws(CompilerError) -> String {
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
    public static func toYAML(_ config: CompilerConfig) throws(CompilerError) -> String {
        // Encode `CompilerConfig` directly via `YAMLEncoder` - it goes through the same
        // `Encodable.encode(to:)` conformance `toJSON`/`toTOML` do (including that method's
        // selective omission of empty optional fields), so there's no need to round-trip
        // through JSON first. An earlier version of this function did round-trip through
        // `JSONSerialization.jsonObject` + `Yams.dump(object:)` to get JSON-like key ordering,
        // but `Yams.dump(object:)` operates on a type-erased `Any` tree and can fail to
        // represent values that came back from `JSONSerialization` as Foundation bridging
        // types (e.g. `NSString`) rather than native Swift ones - encoding straight from the
        // `Encodable` value avoids that failure mode entirely, per `testToYAMLRoundTrip`.
        do {
            return try YAMLEncoder().encode(config)
        } catch let error as CompilerError {
            throw error
        } catch {
            throw CompilerError.serialization(String(describing: error))
        }
    }

    /// Serializes a configuration back to a TOML string.
    public static func toTOML(_ config: CompilerConfig) throws(CompilerError) -> String {
        do {
            return try TOMLEncoder().encode(config)
        } catch {
            throw CompilerError.serialization(String(describing: error))
        }
    }
}

/// Strips `//` line comments and `/* */` block comments from JSONC content so `.json`/`.jsonc`
/// configs with comments can be decoded by `JSONDecoder`, which otherwise rejects them
/// outright. String literals (including escaped quotes) are left untouched so a `//` or `/*`
/// inside a string value is never mistaken for a comment.
func stripJSONCComments(_ content: String) throws(CompilerError) -> String {
    var result = String.UnicodeScalarView()
    var scalars = content.unicodeScalars.makeIterator()
    var pending = scalars.next()

    func advance() -> Unicode.Scalar? {
        let current = pending
        pending = scalars.next()
        return current
    }

    var inString = false
    var escaped = false

    while let scalar = advance() {
        if inString {
            result.append(scalar)
            if escaped {
                escaped = false
            } else if scalar == "\\" {
                escaped = true
            } else if scalar == "\"" {
                inString = false
            }
            continue
        }

        if scalar == "\"" {
            inString = true
            result.append(scalar)
            continue
        }

        if scalar == "/", let next = pending {
            if next == "/" {
                _ = advance()
                while let commentScalar = pending, commentScalar != "\n" {
                    _ = advance()
                }
                continue
            }
            if next == "*" {
                _ = advance()
                var previous: Unicode.Scalar?
                var closed = false
                while let commentScalar = advance() {
                    if previous == "*", commentScalar == "/" {
                        closed = true
                        break
                    }
                    previous = commentScalar
                }
                guard closed else {
                    throw CompilerError.parseFailed(
                        format: "JSON",
                        underlying: "unterminated block comment (/* without a matching */)"
                    )
                }
                // Leave a space where the comment was: `{"n": 1/*c*/0}` would otherwise
                // collapse to `{"n": 10}`, silently merging two adjacent tokens into one.
                // An extra space is always harmless in JSON; a dropped one can corrupt data.
                result.append(" ")
                continue
            }
        }

        result.append(scalar)
    }

    return String(result)
}
