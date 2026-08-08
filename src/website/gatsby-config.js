/**
 * Gatsby configuration for Ad-Blocking documentation website
 * @type {import('gatsby').GatsbyConfig}
 */
module.exports = {
  siteMetadata: {
    title: `Bloqr Core`,
    description: `The core open-source components that form the foundation of Bloqr AI`,
    author: `Bloqr Systems`,
    siteUrl: `https://bloqrai.github.io/bloqr-core/`,
  },
  pathPrefix: `/bloqr-core`,
  plugins: [
    `gatsby-plugin-image`,
    `gatsby-plugin-sharp`,
    `gatsby-transformer-sharp`,
    {
      resolve: `gatsby-source-filesystem`,
      options: {
        name: `docs`,
        path: `${__dirname}/../../docs`,
      },
    },
    {
      resolve: `gatsby-source-filesystem`,
      options: {
        name: `root-docs`,
        path: `${__dirname}/../..`,
        ignore: [
          `**/node_modules/**`,
          `**/.*`,
          `**/data/**`,
          `**/src/**`,
          `**/.github/**`,
          `**/tools/**`,
          `**/api/**`,
          `**/target/**`,
          `**/bin/**`,
          `**/obj/**`,
        ],
      },
    },
    {
      resolve: `gatsby-transformer-remark`,
      options: {
        plugins: [],
      },
    },
    {
      resolve: `gatsby-plugin-manifest`,
      options: {
        name: `Bloqr Core`,
        short_name: `Bloqr`,
        start_url: `/`,
        background_color: `#070B14`,
        theme_color: `#FF5500`,
        display: `minimal-ui`,
        icon: `src/images/icon.svg`,
      },
    },
  ],
}
