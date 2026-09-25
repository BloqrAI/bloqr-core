import Foundation

/// Supported configuration file formats.
///
/// JSON is the only documented format across every wrapper in this repository; YAML/TOML
/// remain functionally supported here (matching the other four language wrappers) for
/// backward compatibility only. See `docs/guides/migration-guide.md`.
public enum ConfigFormat: String, Sendable, Equatable {
    case json
    case yaml
    case toml

    public static func from(fileExtension ext: String) throws(CompilerError) -> ConfigFormat {
        switch ext.lowercased() {
        case "json", "jsonc": return .json
        case "yaml", "yml": return .yaml
        case "toml": return .toml
        default: throw CompilerError.unknownExtension(fileExtension: ext)
        }
    }

    public static func from(path: URL) throws(CompilerError) -> ConfigFormat {
        try from(fileExtension: path.pathExtension)
    }

    public var displayName: String {
        switch self {
        case .json: return "JSON"
        case .yaml: return "YAML"
        case .toml: return "TOML"
        }
    }
}

/// Which compilation engine/grammar a source or configuration uses.
///
/// - `dns` - the DNS/hosts-style grammar (domains, `||domain^`, hosts-file syntax) consumed by
///   DNS resolvers (AdGuard Home/DNS, Pi-hole).
/// - `browser` - AdGuard's browser-syntax grammar (network rules with URL/resource modifiers,
///   cosmetic/element-hiding rules, extended CSS, scriptlets) consumed by browser extensions.
///
/// See `docs/architecture/dual-engine-compilation.md`.
public enum EngineKind: String, Codable, Sendable, Equatable {
    case dns
    case browser

    public init?(argument: String) {
        switch argument.lowercased() {
        case "dns": self = .dns
        case "browser": self = .browser
        default: return nil
        }
    }
}

/// Source type for filter lists.
public enum SourceType: String, Codable, Sendable, Equatable {
    case adblock
    case hosts

    public init(from decoder: Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        switch raw.lowercased() {
        case "adblock": self = .adblock
        case "hosts": self = .hosts
        default:
            throw DecodingError.dataCorruptedError(
                in: try decoder.singleValueContainer(),
                debugDescription: "unknown source type \"\(raw)\""
            )
        }
    }
}

/// A source filter list to compile.
public struct FilterSource: Codable, Sendable, Equatable {
    public var name: String
    public var source: String
    public var type: SourceType
    /// Which compilation engine/grammar this source uses. When set, overrides
    /// auto-detection. When unset, the engine is auto-detected from the source content,
    /// falling back to `CompilerConfig.defaultEngine`, falling back to `.dns`.
    public var engine: EngineKind?
    public var transformations: [String]
    public var inclusions: [String]
    public var exclusions: [String]

    public init(
        name: String = "",
        source: String,
        type: SourceType = .adblock,
        engine: EngineKind? = nil,
        transformations: [String] = [],
        inclusions: [String] = [],
        exclusions: [String] = []
    ) {
        self.name = name
        self.source = source
        self.type = type
        self.engine = engine
        self.transformations = transformations
        self.inclusions = inclusions
        self.exclusions = exclusions
    }

    public var isURL: Bool {
        source.hasPrefix("http://") || source.hasPrefix("https://")
    }

    public var isLocal: Bool { !isURL }

    enum CodingKeys: String, CodingKey {
        case name, source, type, engine, transformations, inclusions, exclusions
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        name = try container.decodeIfPresent(String.self, forKey: .name) ?? ""
        source = try container.decodeIfPresent(String.self, forKey: .source) ?? ""
        type = try container.decodeIfPresent(SourceType.self, forKey: .type) ?? .adblock
        engine = try container.decodeIfPresent(EngineKind.self, forKey: .engine)
        transformations = try container.decodeIfPresent([String].self, forKey: .transformations) ?? []
        inclusions = try container.decodeIfPresent([String].self, forKey: .inclusions) ?? []
        exclusions = try container.decodeIfPresent([String].self, forKey: .exclusions) ?? []
    }
}

/// Configuration for the compiler, matching `schemas/compiler-config.schema.json`.
public struct CompilerConfig: Codable, Sendable, Equatable {
    public var name: String
    public var description: String
    public var homepage: String
    public var license: String
    public var version: String
    public var sources: [FilterSource]
    /// Default compilation engine/grammar for sources that don't set their own `engine`
    /// explicitly and whose content can't be confidently auto-detected. Defaults to `.dns`.
    public var defaultEngine: EngineKind?
    public var transformations: [String]
    public var inclusions: [String]
    public var exclusions: [String]

    /// Format the configuration was read from. Not part of the schema; not serialized.
    public var sourceFormat: ConfigFormat?
    /// Path the configuration was read from. Not part of the schema; not serialized.
    public var sourcePath: URL?

    public init(
        name: String,
        description: String = "",
        homepage: String = "",
        license: String = "",
        version: String = "",
        sources: [FilterSource] = [],
        defaultEngine: EngineKind? = nil,
        transformations: [String] = [],
        inclusions: [String] = [],
        exclusions: [String] = []
    ) {
        self.name = name
        self.description = description
        self.homepage = homepage
        self.license = license
        self.version = version
        self.sources = sources
        self.defaultEngine = defaultEngine
        self.transformations = transformations
        self.inclusions = inclusions
        self.exclusions = exclusions
        self.sourceFormat = nil
        self.sourcePath = nil
    }

    enum CodingKeys: String, CodingKey {
        case name, description, homepage, license, version, sources
        case defaultEngine
        case transformations, inclusions, exclusions
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        name = try container.decodeIfPresent(String.self, forKey: .name) ?? ""
        description = try container.decodeIfPresent(String.self, forKey: .description) ?? ""
        homepage = try container.decodeIfPresent(String.self, forKey: .homepage) ?? ""
        license = try container.decodeIfPresent(String.self, forKey: .license) ?? ""
        version = try container.decodeIfPresent(String.self, forKey: .version) ?? ""
        sources = try container.decodeIfPresent([FilterSource].self, forKey: .sources) ?? []
        defaultEngine = try container.decodeIfPresent(EngineKind.self, forKey: .defaultEngine)
        transformations = try container.decodeIfPresent([String].self, forKey: .transformations) ?? []
        inclusions = try container.decodeIfPresent([String].self, forKey: .inclusions) ?? []
        exclusions = try container.decodeIfPresent([String].self, forKey: .exclusions) ?? []
        sourceFormat = nil
        sourcePath = nil
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        if !description.isEmpty { try container.encode(description, forKey: .description) }
        if !homepage.isEmpty { try container.encode(homepage, forKey: .homepage) }
        if !license.isEmpty { try container.encode(license, forKey: .license) }
        if !version.isEmpty { try container.encode(version, forKey: .version) }
        try container.encode(sources, forKey: .sources)
        try container.encodeIfPresent(defaultEngine, forKey: .defaultEngine)
        if !transformations.isEmpty { try container.encode(transformations, forKey: .transformations) }
        if !inclusions.isEmpty { try container.encode(inclusions, forKey: .inclusions) }
        if !exclusions.isEmpty { try container.encode(exclusions, forKey: .exclusions) }
    }

    /// Transformations this wrapper's `@bloqr/compiler-core` shell-out actually implements.
    /// Mirrors the RemoveComments..ConvertToAscii set documented in CLAUDE.md and the shared
    /// `schemas/compiler-config.schema.json` enum. Deliberately excludes `ConflictDetection`/
    /// `RuleOptimizer`, which are commercial-only browser-engine transformations not
    /// implemented anywhere in this OSS repo (see issue #502).
    public static let validTransformations: Set<String> = [
        "RemoveComments", "Compress", "RemoveModifiers", "Validate", "ValidateAllowIp",
        "Deduplicate", "InvertAllow", "RemoveEmptyLines", "TrimLines", "InsertFinalNewLine",
        "ConvertToAscii",
    ]

    /// Validates the minimum shape a configuration needs before compiling.
    ///
    /// Schema validation runs first so this covers every caller of `validate()` - not just
    /// `ConfigReader.readConfig()`'s file-based path, which schema-validates independently -
    /// including a directly constructed `CompilerConfig` that never went through a file at all.
    public func validate() throws(CompilerError) {
        try SchemaValidation.assertValid(self)

        if name.isEmpty {
            throw CompilerError.validationFailed("configuration 'name' is required")
        }
        if sources.isEmpty {
            throw CompilerError.validationFailed("at least one source is required")
        }
        for (index, source) in sources.enumerated() where source.source.isEmpty {
            throw CompilerError.validationFailed("source[\(index)].source is required")
        }
        for transformation in transformations where !Self.validTransformations.contains(transformation) {
            throw CompilerError.validationFailed(
                "transformations: invalid transformation '\(transformation)'"
            )
        }
        for (index, source) in sources.enumerated() {
            for transformation in source.transformations
            where !Self.validTransformations.contains(transformation) {
                throw CompilerError.validationFailed(
                    "source[\(index)].transformations: invalid transformation '\(transformation)'"
                )
            }
        }
    }

    public var localSourcesCount: Int { sources.filter { $0.isLocal }.count }
    public var remoteSourcesCount: Int { sources.filter { $0.isURL }.count }
}
