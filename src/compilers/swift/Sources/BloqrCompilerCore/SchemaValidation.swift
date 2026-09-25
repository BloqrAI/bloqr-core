import Foundation
import JSONSchema

/// JSON Schema (draft-07) validation for parsed configuration.
///
/// Validates against the same canonical schema (`schemas/compiler-config.schema.json` at the
/// repo root, bundled here as a Swift Package resource so this target carries its own copy)
/// that the .NET, TypeScript, Python, and Rust compilers all validate against - see issue #518
/// (follow-up to #502). Kept in sync with the repo-root schema by a test that diffs the two
/// files.
enum SchemaValidation {
    /// Raw bundled schema text (from `Sources/BloqrCompilerCore/compiler-config.schema.json`),
    /// exposed (module-internal, not `private`) so `SchemaValidationTests`'s drift guard can
    /// compare it against the repo-root canonical file without needing its own `Bundle.module`
    /// (test targets don't get one unless they declare their own resources).
    static let schemaJSONText: String = {
        guard
            let url = Bundle.module.url(forResource: "compiler-config.schema", withExtension: "json"),
            let text = try? String(contentsOf: url, encoding: .utf8)
        else {
            fatalError("bundled compiler-config.schema.json is missing")
        }
        return text
    }()

    /// Lazily parsed once; the schema itself never changes at runtime.
    ///
    /// `nonisolated(unsafe)`: `[String: Any]` isn't `Sendable`, but this value is only ever
    /// written once, by this initializer closure, before any code can read it (Swift's static
    /// `let` initialization is itself synchronized) - safe to read concurrently thereafter.
    nonisolated(unsafe) private static let schema: [String: Any] = {
        guard
            let data = schemaJSONText.data(using: .utf8),
            let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else {
            fatalError("bundled compiler-config.schema.json is not a valid JSON object")
        }
        return object
    }()

    /// Validates a `CompilerConfig` against the canonical JSON Schema and throws a
    /// `CompilerError.validationFailed` with every violation if it doesn't conform.
    ///
    /// This is deliberately independent of, and runs ahead of, `CompilerConfig.validate()`: it
    /// catches structural/type/enum/format errors against the schema documented in
    /// `docs/configuration-reference.md` and shared with every other compiler wrapper, while
    /// `CompilerConfig.validate()` continues to catch anything specific to this wrapper - in
    /// particular, this OSS engine's narrower transformation set (it rejects
    /// `ConflictDetection`/`RuleOptimizer`, which the canonical schema allows because the
    /// TypeScript reference implementation supports them).
    ///
    /// Note: this validates the `CompilerConfig` value re-encoded to a JSON-compatible form
    /// (mirroring what `ConfigReader.toJSON` would produce), not the raw file content -
    /// `CompilerConfig`'s `Decodable` conformance already silently drops any top-level key it
    /// doesn't model (e.g. `output`, `chunking`, `extensions`), so a config using only fields
    /// this wrapper doesn't implement can't be flagged as unrecognized here either way.
    static func assertValid(_ config: CompilerConfig) throws(CompilerError) {
        let data: Data
        do {
            data = try JSONEncoder().encode(config)
        } catch {
            throw CompilerError.serialization(
                "failed to encode configuration for schema validation: \(error)"
            )
        }

        guard let value = try? JSONSerialization.jsonObject(with: data) else {
            throw CompilerError.serialization(
                "failed to prepare encoded configuration for schema validation"
            )
        }

        try assertValid(rawValue: value)
    }

    /// Validates an already-parsed JSON-compatible value (a `[String: Any]`/array/scalar tree,
    /// as produced by `JSONSerialization`) directly against the schema, without going through
    /// `CompilerConfig`'s `Decodable` conformance first.
    ///
    /// This matters because `CompilerConfig`/`FilterSource` use explicit `CodingKeys` that omit
    /// several schema properties (`output`, `hashVerification`, `archiving`, `chunking`,
    /// `extensions`, `$schema`, per-source `useBrowser`) - `Decodable` silently drops any key it
    /// doesn't model, so validating only the re-encoded `CompilerConfig` (as `assertValid(_:)`
    /// above does) can't catch a config that misuses one of those keys or adds an unrecognized
    /// one. `ConfigReader.readConfig` calls this on the raw parsed JSON ahead of decoding for
    /// that reason, in addition to the model-level check every caller of `validate()` gets.
    static func assertValid(rawValue value: Any) throws(CompilerError) {
        let result: ValidationResult
        do {
            result = try JSONSchema.validate(value, schema: schema)
        } catch {
            throw CompilerError.validationFailed(
                "schema validation could not run: \(error)"
            )
        }

        if !result.valid {
            let messages = (result.errors ?? []).map { error in
                "\(error.instanceLocation.path.isEmpty ? "(root)" : error.instanceLocation.path): \(error.description)"
            }
            throw CompilerError.validationFailed(messages.joined(separator: "\n"))
        }
    }
}
