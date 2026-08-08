import * as React from "react"
import { Link, useStaticQuery, graphql } from "gatsby"
import "../styles/global.css"
import ThemeToggle from "./ThemeToggle"
import Search from "./Search"
import bloqrMark from "../images/brand/bloqr-mark.svg"

const Layout = ({ children, pageTitle }) => {
  const data = useStaticQuery(graphql`
    query SearchIndexQuery {
      allMarkdownRemark {
        nodes {
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
  `)

  const searchIndex = data.allMarkdownRemark.nodes.map((node) => ({
    slug: node.fields.slug,
    title: node.frontmatter.title || node.fields.slug,
    excerpt: node.excerpt,
  }))

  return (
    <>
      <header>
        <div className="container header-content">
          <Link to="/" className="brand-mark">
            <img src={bloqrMark} alt="" width="24" height="24" />
            <span className="brand-name">Bloqr</span>
            <span className="brand-sub">List Utils</span>
          </Link>
          <div className="header-actions">
            <Search searchIndex={searchIndex} />
            <ThemeToggle />
          </div>
        </div>
      </header>
      <nav>
        <div className="container">
          <ul>
            <li>
              <Link to="/">Home</Link>
            </li>
            <li>
              <Link to="/getting-started">Getting Started</Link>
            </li>
            <li>
              <Link to="/security">Security</Link>
            </li>
            <li>
              <Link to="/docs">Documentation</Link>
            </li>
            <li>
              <Link to="/guides">Guides</Link>
            </li>
            <li>
              <Link to="/api">API Reference</Link>
            </li>
            <li>
              <Link to="/benchmarks">Benchmarks</Link>
            </li>
            <li>
              <Link to="/improvements">Recent Improvements</Link>
            </li>
          </ul>
        </div>
      </nav>
      <main>
        <div className="container">
          {pageTitle && <h1>{pageTitle}</h1>}
          {children}
        </div>
      </main>
      <footer>
        <div className="container">
          <p>
            Bloqr List Utils - Licensed under{" "}
            <a href="https://github.com/BloqrAI/bloqr-lists/blob/main/LICENSE">
              GPL-3.0
            </a>
          </p>
          <p>
            <a href="https://github.com/BloqrAI/bloqr-lists">
              View on GitHub
            </a>
          </p>
        </div>
      </footer>
    </>
  )
}

export default Layout
