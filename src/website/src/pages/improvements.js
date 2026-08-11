import * as React from "react"
import { Link, graphql } from "gatsby"
import Layout from "../components/Layout"

const ImprovementsPage = ({ data }) => {
  const improvements = [
    {
      title: "Phase 2 Implementation",
      slug: "/PHASE2_IMPLEMENTATION",
      description:
        "Shell reorganization and environment variable standardization across all components.",
    },
    {
      title: "Rust Modernization",
      slug: "/RUST_MODERNIZATION_SUMMARY",
      description:
        "Unified Rust workspace with shared dependencies and improved build performance.",
    },
    {
      title: "Test Infrastructure Updates",
      slug: "/TEST_UPDATES_SUMMARY",
      description:
        "Enhanced testing across all language ecosystems with comprehensive coverage.",
    },
    {
      title: "Runtime Enforcement",
      slug: "/RUNTIME_ENFORCEMENT",
      description:
        "Cryptographic validation ensuring security measures are actually executed.",
    },
    {
      title: "Validation Security",
      slug: "/VALIDATION_ENFORCEMENT",
      description:
        "Enhanced security validation with hash verification and URL security checks.",
    },
    {
      title: "Environment Variable Migration",
      slug: "/ENVIRONMENT_VARIABLE_MIGRATION",
      description:
        "Standardized environment variables across .NET, TypeScript, Rust, and PowerShell.",
    },
  ]

  return (
    <Layout pageTitle="Recent Improvements">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        Discover the latest enhancements and features added to the AdGuard
        Tools and Utilities over the past few weeks.
      </p>

      <div className="features">
        {improvements.map((item) => (
          <div key={item.slug} className="feature">
            <h3>
              <Link to={item.slug}>{item.title}</Link>
            </h3>
            <p>{item.description}</p>
          </div>
        ))}
      </div>

      <section style={{ marginTop: "3rem" }}>
        <h2>All Technical Documentation</h2>
        <ul className="doc-list">
          {data.allMarkdownRemark.nodes.map((node) => (
            <li key={node.id}>
              <Link to={node.fields.slug}>
                {node.frontmatter.title || node.fields.slug}
              </Link>
              {node.excerpt && <p>{node.excerpt}</p>}
            </li>
          ))}
        </ul>
      </section>
    </Layout>
  )
}

export const query = graphql`
  query {
    allMarkdownRemark(
      filter: {
        fileAbsolutePath: {
          regex: "/(PHASE2|RUST|TEST|RUNTIME|VALIDATION|ENVIRONMENT|RESTRUCTURING|AGENTS|WARP|DENO|TYPESCRIPT)/"
        }
      }
      sort: { frontmatter: { title: ASC } }
    ) {
      nodes {
        id
        fields {
          slug
        }
        frontmatter {
          title
        }
        excerpt(pruneLength: 200)
      }
    }
  }
`

export default ImprovementsPage

export const Head = () => (
  <title>Recent Improvements - Bloqr Core</title>
)
