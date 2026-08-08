/**
 * Gatsby configuration for Ad-Blocking documentation website
 * @type {import('gatsby').GatsbyConfig}
 */
module.exports = {
  siteMetadata: {
    title: `Bloqr List Utils`,
    description: `A comprehensive multi-language toolkit for ad-blocking, network protection, and AdGuard DNS management`,
    author: `Bloqr Systems`,
    siteUrl: `https://bloqrai.github.io/bloqr-lists/`,
  },
  pathPrefix: `/bloqr-lists`,
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
        name: `Bloqr List Utils`,
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
