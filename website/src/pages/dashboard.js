import * as React from "react"
import { Link } from "gatsby"
import Layout from "../components/Layout"

const DashboardPage = () => {
  return (
    <Layout pageTitle="Bloqr Dashboard">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        The flagship .NET console app that ties the rest of the toolkit
        together — a single pane of glass for generating and editing
        compiler configs, running compilations with rich live progress,
        and validating your filter lists.
      </p>

      <section>
        <h2>Overview</h2>
        <p>
          Dashboard lives at{" "}
          <a
            href="https://github.com/BloqrAI/bloqr-core/tree/main/src/bloqr-dashboard"
            target="_blank"
            rel="noopener noreferrer"
          >
            src/bloqr-dashboard/
          </a>{" "}
          as its own .NET solution (<code>BloqrDashboard.slnx</code>). It's
          menu-driven and never terminates on an unhandled error — the goal
          is a console app comfortable for non-technical users, while still
          exposing every operation as a CLI switch for automation and as an
          embeddable library (<code>IDashboardService</code>) for anything
          that wants to drive it programmatically. A full .NET MAUI UI is
          planned on top of the same library boundary.
        </p>
        <p>
          Dashboard shares its compiler configuration, hash-verification,
          and event-pipeline logic with the .NET rules compiler via the
          common{" "}
          <Link to="/docs">
            <code>Bloqr.Compiler.Abstractions</code>/<code>Bloqr.Compiler.Core</code>
          </Link>{" "}
          library, rather than duplicating it — so a config generated in
          Dashboard is guaranteed compatible with every compiler in this
          repo, not just the .NET one.
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Key Features</h2>
        <div className="features">
          <div className="feature">
            <h3>Config Generation Wizard</h3>
            <p>
              Walks through every option in the compiler-config schema —
              sources, transformations, inclusions/exclusions, hash
              verification, archiving — and writes a schema-linked, heavily
              commented <code>.jsonc</code> file.
            </p>
          </div>
          <div className="feature">
            <h3>Round-Trip Config Editing</h3>
            <p>
              Open an existing config, edit it in a structured or raw view,
              and save back — with automatic backups and a git-based version
              history you can browse and restore from.
            </p>
          </div>
          <div className="feature">
            <h3>Live Compilation Progress</h3>
            <p>
              Stage-by-stage progress (validation, linting, chunking,
              merging) with rule counts, transformation results, and
              color-coded errors — driven by the same event pipeline the
              compiler itself raises.
            </p>
          </div>
          <div className="feature">
            <h3>Profiles</h3>
            <p>
              Save collections of settings and compiler configs as named
              profiles you can switch between, instead of re-entering the
              same options every run.
            </p>
          </div>
          <div className="feature">
            <h3>Validation &amp; Diagnostics</h3>
            <p>
              Runs the same{" "}
              <a
                href="https://crates.io/crates/bloqr-validator-core"
                target="_blank"
                rel="noopener noreferrer"
              >
                bloqr-validator-core
              </a>{" "}
              hash-verification and syntax-linting library the compilers use
              (via P/Invoke), plus self-diagnostics for fixable config/binary
              problems.
            </p>
          </div>
          <div className="feature">
            <h3>Structured Logging</h3>
            <p>
              JSON logs with configurable verbosity and automatic rollover,
              viewable in human-readable form from within Dashboard itself —
              filterable by app and time range.
            </p>
          </div>
          <div className="feature">
            <h3>CLI &amp; Library Parity</h3>
            <p>
              Everything reachable from the interactive menus is also a CLI
              switch (<code>bloqr-dashboard compile</code>,{" "}
              <code>validate</code>, <code>profiles</code>, …) and an{" "}
              <code>IDashboardService</code> method for embedding.
            </p>
          </div>
          <div className="feature">
            <h3>Durable Event Pipeline</h3>
            <p>
              Polly-backed retry and optional background queueing on
              compilation events, so a slow downstream handler (a log
              sink, a webhook) can't stall or crash a compile.
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Quick Start</h2>
        <pre style={{ marginTop: "0.5rem", marginBottom: "1.5rem" }}>
          <code>{`cd src/bloqr-dashboard
dotnet restore BloqrDashboard.slnx
dotnet run --project src/Bloqr.Dashboard.Console

# Interactive mode is the default; or drive it directly:
dotnet run --project src/Bloqr.Dashboard.Console -- compile --config compiler-config.jsonc
dotnet run --project src/Bloqr.Dashboard.Console -- validate --config compiler-config.jsonc
dotnet run --project src/Bloqr.Dashboard.Console -- profiles list`}</code>
        </pre>
        <p>
          Dashboard's own settings persist in a <code>.jsonc</code> profile
          file (see <code>BLOQR_DASHBOARD_CONFIG</code> /{" "}
          <code>BLOQR_DASHBOARD_CONFIG_DIR</code> in the{" "}
          <Link to="/docs">Environment Variables reference</Link> to
          relocate it).
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Learn More</h2>
        <p>
          The full user guide — installation, every CLI flag, config
          locations, the library-embedding pattern, and troubleshooting —
          lives in the docs:{" "}
          <Link to="/guides/dashboard-guide/">Dashboard Guide →</Link>
        </p>
      </section>
    </Layout>
  )
}

export default DashboardPage

export const Head = () => <title>Bloqr Dashboard - Bloqr Core</title>
