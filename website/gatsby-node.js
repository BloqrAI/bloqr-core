const path = require(`path`)
const { createFilePath } = require(`gatsby-source-filesystem`)

exports.onCreateNode = ({ node, getNode, actions }) => {
  const { createNodeField } = actions
  
  if (node.internal.type === `MarkdownRemark`) {
    const slug = createFilePath({ node, getNode, basePath: `docs` })
    createNodeField({
      node,
      name: `slug`,
      value: slug,
    })
  }
}

exports.createPages = async ({ graphql, actions }) => {
  const { createPage } = actions
  
  const result = await graphql(`
    query {
      allMarkdownRemark {
        nodes {
          id
          fields {
            slug
          }
        }
      }
    }
  `)

  const docTemplate = path.resolve(`./src/templates/doc.js`)
  
  result.data.allMarkdownRemark.nodes.forEach((node) => {
    createPage({
      path: node.fields.slug,
      component: docTemplate,
      context: {
        id: node.id,
      },
    })
  })
}
