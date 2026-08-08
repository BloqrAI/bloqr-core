import * as React from "react"
import { Link } from "gatsby"
import Layout from "../components/Layout"

const IndexPage = () => {
  return (
    <Layout>
      <div className="hero">
        <h1>Bloqr List Utils</h1>
        <p>
          A comprehensive multi-language toolkit for ad-blocking, network
          protection, and AdGuard DNS management.
        </p>
      </div>

      <section>
        <h2>What is Bloqr List Utils?</h2>
        <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
          This toolkit helps you protect your network from ads, trackers, and
          malware. It works with IoT devices, smart TVs, and any device on your
          network — no software installation needed on individual devices.
        </p>

        <div className="features">
          <div className="feature">
            <h3>Network-Wide Protection</h3>
            <p>
              Block ads and trackers across all devices on your network,
              including smart TVs, IoT devices, and mobile phones.
            </p>
          </div>
          <div className="feature">
            <h3>Multiple Languages</h3>
            <p>
              Choose from TypeScript, .NET, Python, Rust, or PowerShell
              compilers - all produce identical results.
            </p>
          </div>
          <div className="feature">
            <h3>Security First</h3>
            <p>
              Built-in validation with SHA-384 hashing protects against
              malicious filter lists and tampering.{" "}
              <Link to="/security">Learn about the 5 layers of protection →</Link>
            </p>
          </div>
          <div className="feature">
            <h3>AdGuard DNS Integration</h3>
            <p>
              Complete API SDKs for managing AdGuard DNS devices, servers, and
              filter lists.
            </p>
          </div>
          <div className="feature">
            <h3>Custom Rules</h3>
            <p>
              Create and manage your own blocking rules with support for both
              adblock and hosts file formats.
            </p>
          </div>
          <div className="feature">
            <h3>Easy to Use</h3>
            <p>
              Interactive launchers, console UIs, and comprehensive
              documentation make getting started simple.
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "3rem" }}>
        <h2>Quick Links</h2>
        <div className="features">
          <div className="feature">
            <h3>
              <Link to="/getting-started">Getting Started</Link>
            </h3>
            <p>
              New to the toolkit? Start here for installation and your first
              compilation.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/security">Security</Link>
            </h3>
            <p>
              Learn how the built-in security features protect against malicious
              filter lists, tampering, and network attacks.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/docs">Documentation</Link>
            </h3>
            <p>
              Comprehensive guides covering all features and components of the
              toolkit.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/benchmarks">Performance Benchmarks</Link>
            </h3>
            <p>Measure and optimize compilation performance with benchmarking tools.</p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/improvements">Recent Improvements</Link>
            </h3>
            <p>See what's new in the latest releases and ongoing development.</p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/adblock-compiler">@bloqr/compiler-core</Link>
            </h3>
            <p>The open-source, dependency-free TypeScript engine that powers every compiler in this repository.</p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "3rem" }}>
        <h2>How It Works</h2>
        <ol style={{ fontSize: "1.1rem", lineHeight: "2" }}>
          <li>
            <strong>Compile Filter Rules:</strong> Use any of the compilers to
            merge and validate blocking lists from multiple sources.
          </li>
          <li>
            <strong>Deploy to AdGuard DNS:</strong> Upload your compiled rules
            to AdGuard DNS using the API clients.
          </li>
          <li>
            <strong>Configure Your Network:</strong> Point your router or
            devices to use AdGuard DNS as their DNS server.
          </li>
          <li>
            <strong>Enjoy Ad-Free Browsing:</strong> All devices on your
            network are now protected from ads and trackers.
          </li>
        </ol>
      </section>
    </Layout>
  )
}

export default IndexPage

export const Head = () => <title>Bloqr List Utils</title>
