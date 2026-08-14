/**
 * Gatsby configuration for the Bloqr Core documentation website.
 *
 * This package lives at the repo root (`website/`), one level up from
 * `src/`, deliberately — it's not one of the compiler wrappers, and the
 * plan is to eventually extract it into its own repository (like
 * `bloqr-apiclients`/`bloqr-blocklists` before it). When that happens,
 * the two `gatsby-source-filesystem` entries below (which currently read
 * `docs/` and other root-level files straight out of the monorepo) will
 * need to switch to a vendoring step (git subtree/submodule, or a sync
 * script that copies `docs/` in at build time) instead of a relative
 * filesystem path, since `docs/` won't be a sibling directory anymore.
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
        path: `${__dirname}/../docs`,
      },
    },
    {
      resolve: `gatsby-source-filesystem`,
      options: {
        name: `root-docs`,
        path: `${__dirname}/..`,
        ignore: [
          `**/node_modules/**`,
          `**/.*`,
          `**/data/**`,
          `**/src/**`,
          `**/website/**`,
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
