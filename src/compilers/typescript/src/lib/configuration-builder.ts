/**
 * ConfigurationBuilder - Fluent builder for creating compiler configurations
 *
 * Provides a programmatic way to create filter list configurations
 * without needing to write JSON/YAML/TOML files.
 *
 * @example
 * ```typescript
 * const config = ConfigurationBuilder.create('My Filter List')
 *   .withDescription('A custom filter list')
 *   .withVersion('1.0.0')
 *   .withLicense('MIT')
 *   .addSource({
 *     source: 'https://example.com/filters.txt',
 *     name: 'Example Filters'
 *   })
 *   .addSource({
 *     source: './local-rules.txt',
 *     type: 'hosts'
 *   })
 *   .withTransformations(['RemoveComments', 'Deduplicate', 'TrimLines'])
 *   .build();
 *
 * const compiler = BloqrCompiler.create();
 * const rules = await compiler.compileFromConfig(config);
 * ```
 */

import type { IConfiguration } from '../index.ts';
import { TransformationType } from '../types/index.ts';

/**
 * Available transformation types.
 *
 * Derived from {@link TransformationType} (rather than a hand-maintained string
 * union) so this public builder API can't silently drift out of sync with the
 * compiler's actual transformation set the way it previously did - this union
 * was missing `ValidateAllowIp` and `ConvertToAscii` entirely before `#512`.
 */
export type Transformation = keyof typeof TransformationType;

/**
 * Source type for filter sources
 */
export type SourceType = 'adblock' | 'hosts';

/**
 * Source configuration for filter sources
 */
export interface SourceConfig {
  /** URL or file path to the filter source */
  source: string;
  /** Optional name for the source */
  name?: string;
  /** Source type (adblock or hosts) */
  type?: SourceType;
  /** Source-specific transformations */
  transformations?: Transformation[];
  /** Source-specific inclusion patterns */
  inclusions?: string[];
  /** Source-specific exclusion patterns */
  exclusions?: string[];
}

/**
 * Internal source representation
 */
interface InternalSource {
  source: string;
  name?: string;
  type?: SourceType;
  transformations?: Transformation[];
  inclusions?: string[];
  exclusions?: string[];
}

/**
 * Builder for creating compiler configurations programmatically
 */
export class ConfigurationBuilder {
  private name: string;
  private description?: string;
  private version?: string;
  private license?: string;
  private homepage?: string;
  private sources: InternalSource[] = [];
  private transformations: Transformation[] = [];
  private inclusions: string[] = [];
  private exclusions: string[] = [];
  private inclusionsSources: string[] = [];
  private exclusionsSources: string[] = [];

  /**
   * Create a new ConfigurationBuilder
   * @param name Filter list name (required)
   */
  constructor(name: string) {
    if (!name || name.trim() === '') {
      throw new Error('Filter list name is required');
    }
    this.name = name;
  }

  /**
   * Create a new ConfigurationBuilder
   * @param name Filter list name
   */
  static create(name: string): ConfigurationBuilder {
    return new ConfigurationBuilder(name);
  }

  /**
   * Set the filter list description
   */
  withDescription(description: string): this {
    this.description = description;
    return this;
  }

  /**
   * Set the filter list version
   */
  withVersion(version: string): this {
    this.version = version;
    return this;
  }

  /**
   * Set the filter list license
   */
  withLicense(license: string): this {
    this.license = license;
    return this;
  }

  /**
   * Set the filter list homepage URL
   */
  withHomepage(homepage: string): this {
    this.homepage = homepage;
    return this;
  }

  /**
   * Add a filter source
   * @param source Source configuration or URL string
   */
  addSource(source: SourceConfig | string): this {
    if (typeof source === 'string') {
      this.sources.push({ source });
    } else {
      this.sources.push(source);
    }
    return this;
  }

  /**
   * Add multiple filter sources
   * @param sources Array of source configurations or URLs
   */
  addSources(sources: (SourceConfig | string)[]): this {
    for (const source of sources) {
      this.addSource(source);
    }
    return this;
  }

  /**
   * Set global transformations
   * @param transformations Array of transformation names
   */
  withTransformations(transformations: Transformation[]): this {
    this.transformations = transformations;
    return this;
  }

  /**
   * Add a transformation to the list
   * @param transformation Transformation name
   */
  addTransformation(transformation: Transformation): this {
    if (!this.transformations.includes(transformation)) {
      this.transformations.push(transformation);
    }
    return this;
  }

  /**
   * Set global inclusion patterns
   * @param patterns Array of regex patterns
   */
  withInclusions(patterns: string[]): this {
    this.inclusions = patterns;
    return this;
  }

  /**
   * Add an inclusion pattern
   * @param pattern Regex pattern
   */
  addInclusion(pattern: string): this {
    this.inclusions.push(pattern);
    return this;
  }

  /**
   * Set global exclusion patterns
   * @param patterns Array of regex patterns
   */
  withExclusions(patterns: string[]): this {
    this.exclusions = patterns;
    return this;
  }

  /**
   * Add an exclusion pattern
   * @param pattern Regex pattern
   */
  addExclusion(pattern: string): this {
    this.exclusions.push(pattern);
    return this;
  }

  /**
   * Set inclusion sources (files containing patterns)
   * @param sources Array of file paths
   */
  withInclusionsSources(sources: string[]): this {
    this.inclusionsSources = sources;
    return this;
  }

  /**
   * Set exclusion sources (files containing patterns)
   * @param sources Array of file paths
   */
  withExclusionsSources(sources: string[]): this {
    this.exclusionsSources = sources;
    return this;
  }

  /**
   * Apply common filter list presets
   * @param preset Preset name
   */
  withPreset(preset: 'basic' | 'strict' | 'minimal'): this {
    switch (preset) {
      case 'basic':
        this.transformations = [
          'RemoveComments',
          'TrimLines',
          'RemoveEmptyLines',
          'Deduplicate',
          'InsertFinalNewLine',
        ];
        break;
      case 'strict':
        this.transformations = [
          'RemoveComments',
          'Compress',
          'Validate',
          'RemoveModifiers',
          'TrimLines',
          'RemoveEmptyLines',
          'Deduplicate',
          'InsertFinalNewLine',
        ];
        break;
      case 'minimal':
        this.transformations = [
          'TrimLines',
          'RemoveEmptyLines',
          'InsertFinalNewLine',
        ];
        break;
    }
    return this;
  }

  /**
   * Build the configuration object
   * @returns IConfiguration compatible with @bloqr/compiler-core
   */
  build(): IConfiguration {
    if (this.sources.length === 0) {
      throw new Error('At least one source is required');
    }

    const config: IConfiguration = {
      name: this.name,
      sources: this.sources.map((s) => ({
        source: s.source,
        name: s.name,
        type: s.type,
        transformations: s.transformations,
        inclusions: s.inclusions,
        exclusions: s.exclusions,
      })) as unknown as IConfiguration['sources'],
    };

    // Add optional fields only if set
    if (this.description) {
      (config as unknown as Record<string, unknown>)['description'] = this.description;
    }
    if (this.version) {
      (config as unknown as Record<string, unknown>)['version'] = this.version;
    }
    if (this.license) {
      (config as unknown as Record<string, unknown>)['license'] = this.license;
    }
    if (this.homepage) {
      (config as unknown as Record<string, unknown>)['homepage'] = this.homepage;
    }
    if (this.transformations.length > 0) {
      config.transformations = this.transformations as unknown as IConfiguration['transformations'];
    }
    if (this.inclusions.length > 0) {
      config.inclusions = this.inclusions;
    }
    if (this.exclusions.length > 0) {
      config.exclusions = this.exclusions;
    }
    if (this.inclusionsSources.length > 0) {
      (config as unknown as Record<string, unknown>)['inclusionsSources'] = this.inclusionsSources;
    }
    if (this.exclusionsSources.length > 0) {
      (config as unknown as Record<string, unknown>)['exclusionsSources'] = this.exclusionsSources;
    }

    return config;
  }

  /**
   * Build and return as JSON string
   */
  toJson(): string {
    return JSON.stringify(this.build(), null, 2);
  }

  /**
   * Clone this builder
   */
  clone(): ConfigurationBuilder {
    const clone = new ConfigurationBuilder(this.name);
    clone.description = this.description;
    clone.version = this.version;
    clone.license = this.license;
    clone.homepage = this.homepage;
    clone.sources = [...this.sources];
    clone.transformations = [...this.transformations];
    clone.inclusions = [...this.inclusions];
    clone.exclusions = [...this.exclusions];
    clone.inclusionsSources = [...this.inclusionsSources];
    clone.exclusionsSources = [...this.exclusionsSources];
    return clone;
  }
}

/**
 * Convenience function to create a ConfigurationBuilder
 */
export function createConfiguration(name: string): ConfigurationBuilder {
  return new ConfigurationBuilder(name);
}

/**
 * All available transformations, derived from {@link TransformationType} so this
 * list can't drift out of sync with the enum it's meant to mirror.
 */
export const AVAILABLE_TRANSFORMATIONS: readonly Transformation[] = Object.values(
  TransformationType,
) as Transformation[];

/**
 * Transformation descriptions for documentation
 */
export const TRANSFORMATION_DESCRIPTIONS: Record<Transformation, string> = {
  RemoveComments: 'Removes all comment lines (! or #)',
  Compress: 'Converts hosts format to adblock syntax',
  RemoveModifiers: 'Removes unsupported modifiers from rules',
  Validate: 'Removes dangerous/incompatible rules',
  ValidateAllowIp: 'Like Validate, but keeps IP-address targets',
  Deduplicate: 'Removes duplicate rules',
  InvertAllow: 'Converts @@exceptions to blocking rules',
  RemoveEmptyLines: 'Removes blank lines',
  TrimLines: 'Trims whitespace from lines',
  InsertFinalNewLine: 'Ensures file ends with newline',
  ConvertToAscii: 'Punycode-encodes non-ASCII domain labels',
  ConflictDetection: 'Detects (logs) conflicting allow/block rule pairs',
  RuleOptimizer: 'Merges redundant element-hiding rules sharing a domain list',
};
