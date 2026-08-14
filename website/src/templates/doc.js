import * as React from "react"
import { Link, graphql } from "gatsby"
import Layout from "../components/Layout"

const DocTemplate = ({ data }) => {
  const doc = data.markdownRemark
  const title = doc.frontmatter.title || "Documentation"

  return (
    <Layout>
      <Link to="/docs" className="back-link">
        ← Back to Documentation
      </Link>
      <div className="doc-content">
        <h1>{title}</h1>
        <div dangerouslySetInnerHTML={{ __html: doc.html }} />
      </div>
    </Layout>
  )
}

export const query = graphql`
  query ($id: String!) {
    markdownRemark(id: { eq: $id }) {
      html
      frontmatter {
        title
      }
      fields {
        slug
      }
    }
  }
`

export const Head = ({ data }) => {
  const title = data.markdownRemark.frontmatter.title || "Documentation"
  return <title>{title} - AdGuard Tools and Utilities</title>
}

export default DocTemplate
