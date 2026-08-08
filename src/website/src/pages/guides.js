import * as React from "react"
import { Link, graphql } from "gatsby"
import Layout from "../components/Layout"

const GuidesPage = ({ data }) => {
  return (
    <Layout pageTitle="User Guides">
      <p style={{ fontSize: "1.1rem", marginBottom: "2rem" }}>
        Step-by-step guides to help you get the most out of the AdGuard Tools
        and Utilities.
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
      filter: { fileAbsolutePath: { regex: "/docs/guides/" } }
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

export default GuidesPage

export const Head = () => <title>Guides - Bloqr List Utils</title>
