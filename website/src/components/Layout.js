import * as React from "react"
import { Link, useStaticQuery, graphql } from "gatsby"
import "../styles/global.css"
import ThemeToggle from "./ThemeToggle"
import Search from "./Search"
import bloqrMark from "../images/brand/bloqr-mark.svg"

const NavIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 6h16M4 12h16M4 18h16" />
  </svg>
)

const MOBILE_NAV_QUERY = "(max-width: 768px)"

const Layout = ({ children, pageTitle }) => {
  const [navOpen, setNavOpen] = React.useState(false)
  const [isMobileNav, setIsMobileNav] = React.useState(false)

  React.useEffect(() => {
    const mediaQuery = window.matchMedia(MOBILE_NAV_QUERY)
    const updateIsMobileNav = (event) => setIsMobileNav(event.matches)
    updateIsMobileNav(mediaQuery)
    mediaQuery.addEventListener("change", updateIsMobileNav)
    return () => mediaQuery.removeEventListener("change", updateIsMobileNav)
  }, [])

  const navCollapsed = isMobileNav && !navOpen

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
            <span className="brand-sub">Core</span>
          </Link>
          <div className="header-actions">
            <Search searchIndex={searchIndex} />
            <ThemeToggle />
            <button
              type="button"
              className="nav-toggle"
              aria-label={navOpen ? "Close navigation menu" : "Open navigation menu"}
              aria-expanded={navOpen}
              aria-controls="site-nav"
              onClick={() => setNavOpen((open) => !open)}
            >
              <NavIcon />
            </button>
          </div>
        </div>
      </header>
      <nav
        id="site-nav"
        className={navOpen ? "nav-open" : undefined}
        inert={navCollapsed ? "" : undefined}
        aria-hidden={navCollapsed ? "true" : undefined}
      >
        <div className="container">
          <ul>
            <li>
              <Link to="/" onClick={() => setNavOpen(false)}>Home</Link>
            </li>
            <li>
              <Link to="/getting-started" onClick={() => setNavOpen(false)}>Getting Started</Link>
            </li>
            <li>
              <Link to="/dashboard" onClick={() => setNavOpen(false)}>Dashboard</Link>
            </li>
            <li>
              <Link to="/security" onClick={() => setNavOpen(false)}>Security</Link>
            </li>
            <li>
              <Link to="/docs" onClick={() => setNavOpen(false)}>Documentation</Link>
            </li>
            <li>
              <Link to="/guides" onClick={() => setNavOpen(false)}>Guides</Link>
            </li>
            <li>
              <Link to="/benchmarks" onClick={() => setNavOpen(false)}>Benchmarks</Link>
            </li>
            <li>
              <Link to="/improvements" onClick={() => setNavOpen(false)}>Recent Improvements</Link>
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
            Bloqr Core - Licensed under{" "}
            <a href="https://github.com/BloqrAI/bloqr-core/blob/main/LICENSE">
              GPL-3.0
            </a>
          </p>
          <p>
            <a href="https://github.com/BloqrAI/bloqr-core">
              View on GitHub
            </a>
          </p>
        </div>
      </footer>
    </>
  )
}

export default Layout
