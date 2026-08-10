import * as React from "react"
import { Link, graphql } from "gatsby"
import Layout from "../components/Layout"

const DocsPage = ({ data }) => {
  // Group docs by category
  const docsByCategory = {
    core: [],
    guides: [],
    technical: [],
  }

  data.allMarkdownRemark.nodes.forEach((node) => {
    const relativePath = node.fields.slug

    if (relativePath.includes("/guides/")) {
      docsByCategory.guides.push(node)
    } else if (
      relativePath.includes("getting-started") ||
      relativePath.includes("compiler-comparison") ||
      relativePath.includes("configuration-reference") ||
      relativePath.includes("docker-guide")
    ) {
      docsByCategory.core.push(node)
    } else {
      docsByCategory.technical.push(node)
    }
  })

  return (
    <Layout pageTitle="Documentation">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        Explore comprehensive documentation for all components of Bloqr Core.
      </p>

      <section style={{ marginBottom: "3rem" }}>
        <h2>Core Documentation</h2>
        <ul className="doc-list">
          {docsByCategory.core.map((node) => (
            <li key={node.id}>
              <Link to={node.fields.slug}>{node.frontmatter.title || node.fields.slug}</Link>
              {node.excerpt && <p>{node.excerpt}</p>}
            </li>
          ))}
        </ul>
      </section>

      <section style={{ marginBottom: "3rem" }}>
        <h2>User Guides</h2>
        <ul className="doc-list">
          {docsByCategory.guides.map((node) => (
            <li key={node.id}>
              <Link to={node.fields.slug}>{node.frontmatter.title || node.fields.slug}</Link>
              {node.excerpt && <p>{node.excerpt}</p>}
            </li>
          ))}
        </ul>
      </section>

      <section style={{ marginBottom: "3rem" }}>
        <h2>Technical Documentation</h2>
        <ul className="doc-list">
          {docsByCategory.technical.map((node) => (
            <li key={node.id}>
              <Link to={node.fields.slug}>{node.frontmatter.title || node.fields.slug}</Link>
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
      filter: { fileAbsolutePath: { regex: "/docs/" } }
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
        fileAbsolutePath
      }
    }
  }
`

export default DocsPage

export const Head = () => <title>Documentation - Bloqr Core</title>
