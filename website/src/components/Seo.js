import * as React from "react"
import { useStaticQuery, graphql } from "gatsby"

/**
 * Shared Open Graph / Twitter Card / canonical metadata for a page's
 * Gatsby `Head` export. Every page's `Head` should render this instead of
 * a bare `<title>`, so link previews on GitHub, Slack, Discord, and social
 * platforms render consistently.
 *
 * @param {{ title?: string, description?: string, pathname?: string, image?: string }} props
 *   `title` is appended to the site title (or used alone for the home
 *   page); `pathname` is the page's site-relative path (e.g. `/security`)
 *   used to build the canonical/og:url.
 */
const Seo = ({ title, description, pathname = "", image }) => {
  const { site } = useStaticQuery(graphql`
    query SeoQuery {
      site {
        siteMetadata {
          title
          description
          siteUrl
        }
      }
    }
  `)

  const { title: siteTitle, description: siteDescription, siteUrl } =
    site.siteMetadata

  const fullTitle = title ? `${title} - ${siteTitle}` : siteTitle
  const metaDescription = description || siteDescription
  const url = `${siteUrl}${pathname}`
  const socialImage = `${siteUrl}${image || "/social-preview.png"}`

  return (
    <>
      <title>{fullTitle}</title>
      <meta name="description" content={metaDescription} />
      <link rel="canonical" href={url} />

      {/* Open Graph (Facebook, LinkedIn, Slack, Discord, ...) */}
      <meta property="og:type" content="website" />
      <meta property="og:site_name" content={siteTitle} />
      <meta property="og:title" content={fullTitle} />
      <meta property="og:description" content={metaDescription} />
      <meta property="og:url" content={url} />
      <meta property="og:image" content={socialImage} />
      <meta property="og:image:width" content="1200" />
      <meta property="og:image:height" content="630" />
      <meta property="og:image:alt" content={siteTitle} />

      {/* Twitter Card */}
      <meta name="twitter:card" content="summary_large_image" />
      <meta name="twitter:title" content={fullTitle} />
      <meta name="twitter:description" content={metaDescription} />
      <meta name="twitter:image" content={socialImage} />
    </>
  )
}

export default Seo
