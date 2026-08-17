import * as React from "react"
import { Link } from "gatsby"
import Layout from "../components/Layout"

const AdBlockCompilerPage = () => {
  return (
    <Layout pageTitle="@bloqr/compiler-core">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        The open-source, dependency-free TypeScript filter compilation engine
        that every compiler in this repository dogfoods, published to JSR at
        v1.0.0.
      </p>

      <section>
        <h2>Overview</h2>
        <p>
          <code>@bloqr/compiler-core</code> is this repository's own
          package — its canonical source lives at{" "}
          <a
            href="https://github.com/BloqrAI/bloqr-core/tree/main/src/compilers/typescript"
            target="_blank"
            rel="noopener noreferrer"
          >
            src/compilers/typescript/
          </a>{" "}
          and it's published to{" "}
          <a
            href="https://jsr.io/@bloqr/compiler-core"
            target="_blank"
            rel="noopener noreferrer"
          >
            JSR
          </a>
          . It replaced the AdGuard-maintained{" "}
          <code>@adguard/hostlist-compiler</code> npm package that the .NET,
          Python, and Rust compilers in this repo used to shell out to — they
          now shell out to <code>@bloqr/compiler-core</code> instead, and
          the TypeScript compiler <em>is</em> the package, compiling
          in-process.
        </p>
        <p>
          It's a minimal, dependency-free compilation engine: no{" "}
          <code>@adguard/agtree</code> or other third-party AdGuard library,
          no Cloudflare-specific code, no commercial features. It's
          deliberately kept separate from Bloqr's commercial{" "}
          <code>@bloqr/compiler</code> product, which layers AST tooling,
          linting, plugins, and Cloudflare Workers deployment on top of
          AdGuard libraries.
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Key Features</h2>
        <div className="features">
          <div className="feature">
            <h3>Multi-Source Compilation</h3>
            <p>
              Combine filter lists from URLs and local files with automatic
              format detection and validation.
            </p>
          </div>
          <div className="feature">
            <h3>Dependency-Free Core</h3>
            <p>
              No <code>@adguard/agtree</code> or other third-party AdGuard
              library — rule classification is string/regex-based.
            </p>
          </div>
          <div className="feature">
            <h3>Chunked Parallel Compilation</h3>
            <p>
              For large rule lists (10M+ entries), with SHA-384 hash
              consistency preserved across chunks.
            </p>
          </div>
          <div className="feature">
            <h3>Dependency Injection</h3>
            <p>
              Inject a custom logger and compilation event hooks into{" "}
              <code>FilterCompiler</code> for testability and custom
              logging/monitoring.
            </p>
          </div>
          <div className="feature">
            <h3>Thoroughly Tested</h3>
            <p>
              1008 passing tests, clean type-check, lint, and format checks
              on every change.
            </p>
          </div>
          <div className="feature">
            <h3>Interactive Console</h3>
            <p>
              Menu-driven interactive mode alongside full CLI support — no
              separate web UI needed.
            </p>
          </div>
          <div className="feature">
            <h3>JSR Distribution</h3>
            <p>
              Installable with <code>deno add jsr:@bloqr/compiler-core</code>,
              or run directly with <code>deno run</code> — no npm required.
            </p>
          </div>
          <div className="feature">
            <h3>11 Transformations</h3>
            <p>
              Deduplicate, compress, validate, remove comments, and more—all
              applied in a fixed, documented order.
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Quick Start</h2>

        <h3>Installation</h3>
        <pre style={{ marginTop: "0.5rem", marginBottom: "1.5rem" }}>
          # Install from JSR
          <br />
          deno add jsr:@bloqr/compiler-core
          <br />
          <br />
          # Or run directly without installation
          <br />
          deno run jsr:@bloqr/compiler-core/cli
        </pre>

        <h3>Basic Usage</h3>
        <pre style={{ marginTop: "0.5rem", marginBottom: "1.5rem" }}>
          {`import { compile } from '@bloqr/compiler-core';

// Compile from configuration
const rules = await compile({
  name: "My Filter List",
  sources: [
    {
      source: "https://example.com/filters.txt"
    }
  ],
  transformations: ["Deduplicate", "RemoveEmptyLines"]
});

console.log(\`Compiled \${rules.length} rules\`);`}
        </pre>

        <h3>CLI Usage</h3>
        <pre style={{ marginTop: "0.5rem" }}>
          # Compile from config file
          <br />
          deno run --allow-read --allow-write --allow-env --allow-net --allow-run jsr:@bloqr/compiler-core/cli -c config.json
          <br />
          <br />
          # Show version
          <br />
          deno run jsr:@bloqr/compiler-core/cli --version
        </pre>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>How the Other Compilers Use It</h2>
        <p>
          The TypeScript compiler (<code>src/compilers/typescript/</code>)
          compiles in-process — it <em>is</em> this package. The .NET,
          Python, and Rust compilers shell out to it via Deno instead of
          maintaining their own copy of the engine:
        </p>
        <pre style={{ marginTop: "0.5rem" }}>
          {`deno run --allow-read --allow-write --allow-env --allow-net --allow-run \\
  jsr:@bloqr/compiler-core/cli \\
  --config config.json --output data/output/filters.txt`}
        </pre>
        <p>
          Each compiler keeps its own public API and field names unchanged
          across this migration — only what populates them changed. See the{" "}
          <a
            href="https://github.com/BloqrAI/bloqr-core/blob/main/docs/guides/compiler-core-guide.md"
            target="_blank"
            rel="noopener noreferrer"
          >
            full guide
          </a>{" "}
          for CI/CD integration examples (GitHub Actions, GitLab CI, Jenkins,
          Docker).
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Configuration</h2>
        <p>
          <code>@bloqr/compiler-core</code> supports the same
          configuration schema as AdGuard's <code>hostlist-compiler</code>,
          so existing configurations carry over without changes.
        </p>

        <h3>Configuration Example (JSON)</h3>
        <pre style={{ marginTop: "0.5rem" }}>
          {`{
  "name": "My Filter List",
  "description": "Custom ad-blocking filter",
  "version": "1.0.0",
  "sources": [
    {
      "name": "Local Rules",
      "source": "data/local.txt",
      "type": "adblock"
    },
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "transformations": ["RemoveModifiers", "Validate"]
    }
  ],
  "transformations": ["Deduplicate", "RemoveEmptyLines", "InsertFinalNewLine"],
  "exclusions": ["*.google.com", "/analytics/"]
}`}
        </pre>

        <h3 style={{ marginTop: "1.5rem" }}>Available Transformations</h3>
        <ol style={{ lineHeight: "1.8" }}>
          <li><strong>ConvertToAscii</strong> - Convert internationalized domains</li>
          <li><strong>TrimLines</strong> - Remove whitespace</li>
          <li><strong>RemoveComments</strong> - Strip comments</li>
          <li><strong>Compress</strong> - Convert hosts to adblock syntax</li>
          <li><strong>RemoveModifiers</strong> - Remove unsupported modifiers</li>
          <li><strong>InvertAllow</strong> - Convert to allowlist</li>
          <li><strong>Validate</strong> - Remove invalid rules</li>
          <li><strong>ValidateAllowIp</strong> - Validate but keep IPs</li>
          <li><strong>Deduplicate</strong> - Remove duplicates</li>
          <li><strong>RemoveEmptyLines</strong> - Clean empty lines</li>
          <li><strong>InsertFinalNewLine</strong> - Add final newline</li>
        </ol>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Documentation & Resources</h2>
        <div className="features">
          <div className="feature">
            <h3>
              <a
                href="https://github.com/BloqrAI/bloqr-core/tree/main/src/compilers/typescript"
                target="_blank"
                rel="noopener noreferrer"
              >
                Source Code
              </a>
            </h3>
            <p>
              <code>src/compilers/typescript/</code> in the main repository.
            </p>
          </div>
          <div className="feature">
            <h3>
              <a
                href="https://jsr.io/@bloqr/compiler-core"
                target="_blank"
                rel="noopener noreferrer"
              >
                JSR Package
              </a>
            </h3>
            <p>Install, browse the API reference, and see release history.</p>
          </div>
          <div className="feature">
            <h3>
              <a
                href="https://github.com/BloqrAI/bloqr-core/blob/main/docs/guides/compiler-core-guide.md"
                target="_blank"
                rel="noopener noreferrer"
              >
                Complete Guide
              </a>
            </h3>
            <p>
              Architecture, CI/CD integration examples, migration steps, and
              full API reference.
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Comparison with AdGuard's hostlist-compiler</h2>
        <p>
          <code>@bloqr/compiler-core</code> superseded{" "}
          <code>@adguard/hostlist-compiler</code> across every compiler in
          this repository — not as an npm-compatible fork with a fallback
          path, but as a full replacement.
        </p>

        <table style={{ width: "100%", marginTop: "1rem", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ borderBottom: "2px solid var(--border-color)" }}>
              <th style={{ padding: "0.75rem", textAlign: "left" }}>Feature</th>
              <th style={{ padding: "0.75rem", textAlign: "center" }}>@adguard/hostlist-compiler</th>
              <th style={{ padding: "0.75rem", textAlign: "center" }}>@bloqr/compiler-core</th>
            </tr>
          </thead>
          <tbody>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>Maintained by</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>AdGuard</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>This repo</td>
            </tr>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>Distribution</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>npm</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>JSR</td>
            </tr>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>11 Transformations</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
            </tr>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>Chunked parallel compilation</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✕</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
            </tr>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>Dependency injection</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✕</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
            </tr>
            <tr style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "0.75rem" }}>Interactive console mode</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✕</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
            </tr>
            <tr>
              <td style={{ padding: "0.75rem" }}>We can fix/extend it ourselves</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✕</td>
              <td style={{ padding: "0.75rem", textAlign: "center" }}>✓</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Next Steps</h2>
        <div className="features">
          <div className="feature">
            <h3>
              <Link to="/getting-started">Getting Started</Link>
            </h3>
            <p>
              Learn how to compile your first filter list with any of the
              four compilers.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/compiler-comparison">Compare Compilers</Link>
            </h3>
            <p>
              See how the TypeScript, .NET, Python, and Rust compilers stack
              up.
            </p>
          </div>
          <div className="feature">
            <h3>
              <a
                href="https://github.com/BloqrAI/bloqr-core"
                target="_blank"
                rel="noopener noreferrer"
              >
                View on GitHub
              </a>
            </h3>
            <p>Explore the source code and contribute to development.</p>
          </div>
        </div>
      </section>
    </Layout>
  )
}

export default AdBlockCompilerPage

export const Head = () => <title>@bloqr/compiler-core - Bloqr Core</title>
