import * as React from "react"
import { Link } from "gatsby"
import Layout from "../components/Layout"

const GettingStartedPage = () => {
  return (
    <Layout pageTitle="Getting Started">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        This guide will help get started with the
        Bloqr List Utils in just a few minutes.
      </p>

      <section>
        <h2>Quick Start Options</h2>
        <div className="features">
          <div className="feature">
            <h3>Interactive Launcher (Recommended)</h3>
            <p>The easiest way to get started. Choose your platform:</p>
            <ul>
              <li>
                <strong>Linux/macOS:</strong> Run <code>./launcher.sh</code>
              </li>
              <li>
                <strong>Windows:</strong> Run <code>.\launcher.ps1</code>
              </li>
            </ul>
            <p>
              The launcher provides a guided menu system for all tools and
              tasks.
            </p>
          </div>
          <div className="feature">
            <h3>Choose Your Language</h3>
            <p>Pick the compiler that fits your environment:</p>
            <ul>
              <li>
                <strong>TypeScript/Deno:</strong> Secure by default, no build
                step
              </li>
              <li>
                <strong>.NET:</strong> Cross-platform with excellent tooling
              </li>
              <li>
                <strong>Python:</strong> Easy installation via pip
              </li>
              <li>
                <strong>Rust:</strong> Single binary, no runtime dependencies
              </li>
            </ul>
            <p>
              <Link to="/compiler-comparison">Compare compilers →</Link>
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Prerequisites</h2>
        <p>At least one of these is required:</p>
        <ul style={{ fontSize: "1.1rem", lineHeight: "2" }}>
          <li>
            <strong>Deno 2.0+</strong> for TypeScript compiler
          </li>
          <li>
            <strong>.NET 10 SDK</strong> for .NET compiler and API client
          </li>
          <li>
            <strong>Python 3.9+</strong> for Python compiler
          </li>
          <li>
            <strong>Rust 1.83+</strong> for Rust compiler and tools
          </li>
          <li>
            <strong>PowerShell 7+</strong> for PowerShell scripts
          </li>
        </ul>
        <p>
          All compilers dogfood{" "}
          <Link to="/adblock-compiler">@bloqr/compiler-core</Link>, this
          repository's own open-source, dependency-free compilation engine
          published to JSR.
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Installation Steps</h2>
        <ol style={{ fontSize: "1.1rem", lineHeight: "2" }}>
          <li>
            <strong>Clone the repository:</strong>
            <pre style={{ marginTop: "0.5rem" }}>
              git clone https://github.com/BloqrAI/bloqr-lists.git
              <br />
              cd bloqr-lists
            </pre>
          </li>
          <li>
            <strong>Install dependencies:</strong> Run the build script for your
            chosen language(s):
            <pre style={{ marginTop: "0.5rem" }}>
              # Build all projects
              <br />
              ./build.sh
              <br />
              <br />
              # Or specific language
              <br />
              ./build.sh --typescript
              <br />
              ./build.sh --dotnet
              <br />
              ./build.sh --python
              <br />
              ./build.sh --rust
            </pre>
          </li>
          <li>
            <strong>Compile filter rules:</strong> Choose your preferred method:
            <pre style={{ marginTop: "0.5rem" }}>
              # TypeScript
              <br />
              cd src/adblock-compiler-core && deno task compile
              <br />
              <br />
              # .NET
              <br />
              cd src/rules-compiler-dotnet && dotnet run --project
              src/RulesCompiler.Console
              <br />
              <br />
              # Python
              <br />
              cd src/rules-compiler-python && rules-compiler
              <br />
              <br />
              # Rust
              <br />
              cargo run --release -p rules-compiler
            </pre>
          </li>
        </ol>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Next Steps</h2>
        <div className="features">
          <div className="feature">
            <h3>
              <Link to="/docs">Explore Documentation</Link>
            </h3>
            <p>
              Learn about advanced features, configuration options, and API
              integration.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/guides">Read the Guides</Link>
            </h3>
            <p>
              Step-by-step tutorials for common tasks and deployment scenarios.
            </p>
          </div>
          <div className="feature">
            <h3>
              <a href="https://github.com/BloqrAI/bloqr-lists">
                Join the Community
              </a>
            </h3>
            <p>
              Contribute, report issues, or ask questions on GitHub.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/adblock-compiler">@bloqr/compiler-core</Link>
            </h3>
            <p>
              Learn about the open-source compilation engine that powers every
              compiler in this repository.
            </p>
          </div>
        </div>
      </section>

      <section
        style={{
          marginTop: "2rem",
          padding: "1.5rem",
          backgroundColor: "var(--surface-color)",
          border: "1px solid var(--border-color)",
          borderRadius: "12px",
        }}
      >
        <h2>Need Help?</h2>
        <p>
          For detailed installation instructions and troubleshooting, see the{" "}
          <Link to="/getting-started">complete Getting Started guide</Link> in
          the documentation.
        </p>
      </section>
    </Layout>
  )
}

export default GettingStartedPage

export const Head = () => <title>Getting Started - Bloqr List Utils</title>
