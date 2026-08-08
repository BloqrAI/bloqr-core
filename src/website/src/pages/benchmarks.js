import * as React from "react"
import { Link } from "gatsby"
import Layout from "../components/Layout"

const BenchmarksPage = () => {
  return (
    <Layout pageTitle="Performance Benchmarks">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        Measure and understand the performance characteristics of the filter
        rule compilers, including parallel chunking speedups and cross-language
        comparisons.
      </p>

      <section>
        <h2>Overview</h2>
        <p>
          The repository includes comprehensive benchmarking tools to help
          understand performance across different compilers and optimize
          compilation workflows. All compilers (TypeScript, .NET, Python, Rust)
          support parallel chunking for improved performance with large filter
          lists.
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Benchmarking Tools</h2>
        <div className="features">
          <div className="feature">
            <h3>Quick Synthetic Benchmark</h3>
            <p>
              <strong>File:</strong> <code>benchmarks/quick_benchmark.py</code>
            </p>
            <p>
              Fast simulation showing expected speedups without requiring full
              compilation setup. Demonstrates:
            </p>
            <ul>
              <li>How rules are split into chunks</li>
              <li>Simulated parallel processing time</li>
              <li>Expected speedup ratios</li>
            </ul>
          </div>
          <div className="feature">
            <h3>Full Benchmark Suite</h3>
            <p>
              <strong>Files:</strong> <code>benchmarks/run_benchmarks.py</code>,{" "}
              <code>generate_synthetic_data.py</code>
            </p>
            <p>
              Complete benchmarking across all compilers with real compilation.
              Compares sequential vs chunked/parallel performance using
              synthetic test data.
            </p>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Running Benchmarks</h2>

        <h3>Quick Synthetic Benchmark</h3>
        <p>
          Run a quick simulation to see expected speedups:
        </p>
        <pre style={{ marginTop: "0.5rem" }}>
          cd benchmarks
          <br />
          <br />
          # Run comparison suite (recommended)
          <br />
          python quick_benchmark.py --suite
          <br />
          <br />
          # Run parallel scaling test
          <br />
          python quick_benchmark.py --scaling
          <br />
          <br />
          # Custom benchmark
          <br />
          python quick_benchmark.py --rules 500000 --parallel 8
          <br />
          <br />
          # Interactive mode
          <br />
          python quick_benchmark.py --interactive
        </pre>

        <h3 style={{ marginTop: "2rem" }}>Full Benchmark with Real Compilation</h3>
        <p>
          Generate synthetic test data and run actual compilation benchmarks:
        </p>
        <pre style={{ marginTop: "0.5rem" }}>
          cd benchmarks
          <br />
          <br />
          # Generate test data (small, medium, large, xlarge filter lists)
          <br />
          python generate_synthetic_data.py --all
          <br />
          <br />
          # Run benchmarks across all compilers
          <br />
          python run_benchmarks.py
          <br />
          <br />
          # Run specific compiler only
          <br />
          python run_benchmarks.py --compiler python --iterations 5
          <br />
          <br />
          # Run specific size only
          <br />
          python run_benchmarks.py --size large
        </pre>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Expected Performance</h2>
        <p>
          Performance varies by hardware, I/O speed, and network latency, but
          here are typical results from synthetic benchmarks:
        </p>

        <div style={{ overflowX: "auto", marginTop: "1rem" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ backgroundColor: "var(--table-header-bg)" }}>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Rule Count
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Sequential
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  4 Workers
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  8 Workers
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Speedup (8w)
                </th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>10,000</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~150ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~60ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~40ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}><strong>3.75x</strong></td>
              </tr>
              <tr style={{ backgroundColor: "var(--table-row-even-bg)" }}>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>50,000</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~600ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~200ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~120ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}><strong>5.0x</strong></td>
              </tr>
              <tr>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>200,000</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~2.5s</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~800ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~400ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}><strong>6.25x</strong></td>
              </tr>
              <tr style={{ backgroundColor: "var(--table-row-even-bg)" }}>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>500,000</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~6s</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~1.8s</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>~900ms</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}><strong>6.67x</strong></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Parallel Scaling</h2>
        <p>
          Speedup scales with CPU cores but with diminishing returns due to
          overhead, merge time, and I/O contention:
        </p>

        <div style={{ overflowX: "auto", marginTop: "1rem" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ backgroundColor: "var(--table-header-bg)" }}>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Workers
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Theoretical Max
                </th>
                <th style={{ padding: "0.75rem", textAlign: "left", borderBottom: "2px solid var(--border-color)" }}>
                  Typical Efficiency
                </th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>2</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>2.0x</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>90-100%</td>
              </tr>
              <tr style={{ backgroundColor: "var(--table-row-even-bg)" }}>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>4</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>4.0x</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>85-95%</td>
              </tr>
              <tr>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>8</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>8.0x</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>75-90%</td>
              </tr>
              <tr style={{ backgroundColor: "var(--table-row-even-bg)" }}>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>16</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>16.0x</td>
                <td style={{ padding: "0.75rem", borderBottom: "1px solid #eee" }}>60-80%</td>
              </tr>
            </tbody>
          </table>
        </div>

        <p style={{ marginTop: "1rem", fontStyle: "italic", color: "#666" }}>
          Efficiency decreases due to process startup overhead, merge/deduplication
          time, memory bandwidth limits, and I/O contention.
        </p>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>When to Use Chunking</h2>
        <p>
          Parallel chunking provides the most benefit for large filter lists
          with multiple sources:
        </p>

        <div className="features">
          <div className="feature">
            <h3>Enable Chunking</h3>
            <ul>
              <li>6+ filter sources</li>
              <li>Large combined filter lists (100K+ rules)</li>
              <li>Multi-core systems (4+ cores)</li>
              <li>Build/CI pipelines</li>
            </ul>
          </div>
          <div className="feature">
            <h3>Disable Chunking</h3>
            <ul>
              <li>1-5 filter sources</li>
              <li>Small filter lists (&lt;50K rules)</li>
              <li>Memory-constrained systems</li>
              <li>Network-bound scenarios (slow downloads)</li>
            </ul>
          </div>
        </div>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Example Output</h2>
        <p>
          Here's what you might see from the quick benchmark suite:
        </p>
        <pre style={{ 
          backgroundColor: "#1e1e1e", 
          color: "#d4d4d4", 
          padding: "1rem", 
          borderRadius: "4px",
          overflowX: "auto"
        }}>
{`CHUNKING PERFORMANCE COMPARISON SUITE
======================================================================
CPU cores available: 8
Max parallel workers: 8

Size            Sequential      Parallel        Speedup      Efficiency
----------------------------------------------------------------------
10K rules       150 ms          70 ms           2.14x        27%
50K rules       570 ms          130 ms          4.38x        55%
200K rules      2,350 ms        350 ms          6.71x        84%
500K rules      5,400 ms        800 ms          6.75x        84%
----------------------------------------------------------------------

Average speedup: 5.00x
Maximum speedup: 6.75x`}
        </pre>
      </section>

      <section style={{ marginTop: "2rem" }}>
        <h2>Learn More</h2>
        <div className="features">
          <div className="feature">
            <h3>
              <Link to="/chunking-guide">Chunking Guide</Link>
            </h3>
            <p>
              Complete documentation on parallel chunking including
              configuration, API reference, and best practices.
            </p>
          </div>
          <div className="feature">
            <h3>
              <Link to="/compiler-comparison">Compiler Comparison</Link>
            </h3>
            <p>
              Compare the different compiler implementations and choose the
              best one based on specific needs.
            </p>
          </div>
          <div className="feature">
            <h3>
              <a href="https://github.com/BloqrAI/bloqr-lists/tree/main/benchmarks">
                View Benchmark Code
              </a>
            </h3>
            <p>
              Explore the benchmark scripts on GitHub to understand the
              implementation details.
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
        <h2>Tip</h2>
        <p>
          Run benchmarks on actual hardware to get accurate performance
          data for specific use cases. Results vary based on CPU cores,
          memory, I/O speed, and network latency.
        </p>
      </section>
    </Layout>
  )
}

export default BenchmarksPage

export const Head = () => <title>Performance Benchmarks - Bloqr List Utils</title>
