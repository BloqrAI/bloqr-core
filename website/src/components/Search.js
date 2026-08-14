import * as React from "react"
import { navigate } from "gatsby"

const Search = ({ searchIndex }) => {
  const [query, setQuery] = React.useState("")
  const [results, setResults] = React.useState([])
  const [showResults, setShowResults] = React.useState(false)

  React.useEffect(() => {
    if (query.length < 2) {
      setResults([])
      setShowResults(false)
      return
    }

    // Simple client-side search
    const searchQuery = query.toLowerCase()
    const filtered = searchIndex
      .filter((item) => {
        const titleMatch = item.title?.toLowerCase().includes(searchQuery)
        const excerptMatch = item.excerpt?.toLowerCase().includes(searchQuery)
        const slugMatch = item.slug?.toLowerCase().includes(searchQuery)
        return titleMatch || excerptMatch || slugMatch
      })
      .slice(0, 10)

    setResults(filtered)
    setShowResults(true)
  }, [query, searchIndex])

  const handleSubmit = (e) => {
    e.preventDefault()
    if (results.length > 0) {
      navigate(results[0].slug)
      setQuery("")
      setShowResults(false)
    }
  }

  const handleResultClick = (slug) => {
    navigate(slug)
    setQuery("")
    setShowResults(false)
  }

  return (
    <div className="search-container">
      <form onSubmit={handleSubmit} className="search-form">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onFocus={() => setShowResults(true)}
          placeholder="Search documentation..."
          className="search-input"
          aria-label="Search documentation"
        />
        {query && (
          <button
            type="button"
            onClick={() => {
              setQuery("")
              setShowResults(false)
            }}
            className="search-clear"
            aria-label="Clear search"
          >
            ✕
          </button>
        )}
      </form>
      {showResults && results.length > 0 && (
        <div className="search-results">
          {results.map((result) => (
            <div
              key={result.slug}
              className="search-result"
              onClick={() => handleResultClick(result.slug)}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleResultClick(result.slug)
              }}
              role="button"
              tabIndex={0}
            >
              <div className="search-result-title">{result.title}</div>
              {result.excerpt && (
                <div className="search-result-excerpt">{result.excerpt}</div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export default Search
