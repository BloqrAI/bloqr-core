import * as React from "react"
import { Link, graphql } from "gatsby"
import Layout from "../components/Layout"

const ApiPage = ({ data }) => {
  return (
    <Layout pageTitle="API Reference">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        Complete API reference documentation for AdGuard DNS integration.
      </p>

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
    </Layout>
  )
}

export const query = graphql`
  query {
    allMarkdownRemark(
      filter: { fileAbsolutePath: { regex: "/docs/api/" } }
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

export default ApiPage

export const Head = () => <title>API Reference - Bloqr List Utils</title>
