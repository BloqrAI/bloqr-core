import * as React from "react"

/**
 * Renders every ```mermaid fenced code block inside the given container ref as an
 * actual diagram, client-side only (mermaid needs the DOM, so this never runs during
 * Gatsby's build-time SSR).
 *
 * gatsby-transformer-remark emits fenced code blocks as plain
 * `<pre><code class="language-mermaid">...</code></pre>` - there's no remark/rehype
 * plugin pipeline here (see gatsby-config.js's empty `plugins: []`), so diagram
 * rendering happens by finding those blocks after the markdown HTML is injected and
 * swapping each one for mermaid's rendered SVG. The original source is kept on the
 * rendered wrapper's `data-mermaid-source` so a later theme change can re-render the
 * same diagram without needing the (by then replaced) original <pre><code> back.
 */
export function useMermaidDiagrams(containerRef) {
  React.useEffect(() => {
    if (typeof window === "undefined" || !containerRef.current) {
      return undefined
    }

    let cancelled = false

    const render = async () => {
      const container = containerRef.current
      if (!container) return

      const unrendered = Array.from(container.querySelectorAll("code.language-mermaid")).map((block) => ({
        source: block.textContent || "",
        target: block.closest("pre"),
      }))
      const alreadyRendered = Array.from(
        container.querySelectorAll(".mermaid-diagram[data-mermaid-source]")
      ).map((div) => ({
        source: div.dataset.mermaidSource || "",
        target: div,
      }))
      const items = [...unrendered, ...alreadyRendered].filter((item) => item.target)
      if (items.length === 0) return

      const { default: mermaid } = await import("mermaid")
      if (cancelled) return

      const isDark = document.documentElement.getAttribute("data-theme") !== "light"
      mermaid.initialize({
        startOnLoad: false,
        theme: isDark ? "dark" : "default",
        securityLevel: "strict",
      })

      for (const [index, item] of items.entries()) {
        const id = `mermaid-diagram-${index}-${Math.random().toString(36).slice(2)}`
        try {
          const { svg } = await mermaid.render(id, item.source)
          if (cancelled) return
          const wrapper = document.createElement("div")
          wrapper.className = "mermaid-diagram"
          wrapper.dataset.mermaidSource = item.source
          wrapper.innerHTML = svg
          item.target.replaceWith(wrapper)
        } catch (err) {
          // Leave the original fenced block in place (raw mermaid source, still
          // readable as text) instead of hiding a rendering failure silently.
          // eslint-disable-next-line no-console
          console.error("Mermaid diagram failed to render:", err)
        }
      }
    }

    render()

    // Re-render from the stored source when the user toggles light/dark mode, so
    // diagrams follow ThemeToggle's data-theme attribute instead of staying stuck in
    // whichever theme was active on first render.
    const observer = new MutationObserver((mutations) => {
      if (mutations.some((m) => m.attributeName === "data-theme")) {
        render()
      }
    })
    observer.observe(document.documentElement, { attributes: true })

    return () => {
      cancelled = true
      observer.disconnect()
    }
  }, [containerRef])
}
