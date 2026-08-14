# Bloqr Core Website

Gatsby-powered documentation website for [`BloqrAI/bloqr-core`](https://github.com/BloqrAI/bloqr-core) — guides, API reference, and security docs, generated from `docs/` and this repo's root `README.md`, styled with [Filter](https://github.com/BloqrAI/bloqr-design-system), Bloqr's design system (`src/styles/global.css`).

This package lives at the repo root (`website/`), not under `src/`, deliberately: it isn't one of the compiler wrappers, and it's slated for eventual extraction into its own repository — see [Future extraction](#future-extraction-plan) below.

## Development

```bash
# Install dependencies
npm install

# Start development server
npm run develop

# Build for production
npm run build

# Serve production build
npm run serve
```

## End-to-End Tests

E2E tests use [Playwright](https://playwright.dev/), and build/serve the site locally by default:

```bash
pnpm install
pnpm run test:e2e

# Interactive UI mode
pnpm run test:e2e:ui
```

By default tests launch a local Chromium against `npm run serve` (built via
`gatsby serve`, port 9000). To run the same tests against
[Cloudflare Browser Run](https://developers.cloudflare.com/browser-run/) instead
of a local browser (e.g. in CI, or to test from a non-datacenter IP), set:

```bash
export CLOUDFLARE_BROWSER_RUN_KEY=...
export CLOUDFLARE_BROWSER_RUN_ENDPOINT=wss://your-worker.workers.dev/cdp
pnpm run test:e2e
```

To point tests at an already-running deployment instead of building locally:

```bash
E2E_BASE_URL=https://bloqrai.github.io/bloqr-core pnpm run test:e2e
```

## Structure

- `src/pages/` - Hand-authored pages (home, getting started, Dashboard, security, benchmarks, etc.)
- `src/templates/` - Templates for pages generated from `docs/*.md` (one per markdown file, via `gatsby-node.js`)
- `src/components/` - Reusable React components
  - `Layout.js` - Main layout with header, nav, footer, search, and theme toggle
  - `Search.js` - Client-side search component with autocomplete
  - `ThemeToggle.js` - Dark/light mode toggle with persistence
- `src/styles/` - Global CSS: Filter design tokens (colors, spacing, radii, type) plus dark/light mode
- `src/images/brand/` - Bloqr logo/mark SVGs
- `gatsby-config.js` - Gatsby configuration (see its header comment for the `docs/`-sourcing path and the future-extraction note)
- `gatsby-node.js` - Node APIs for page generation

## Features

- 📚 Automatic documentation pages from `docs/*.md`
- 🔍 Organized by category (Core, Guides, Technical)
- 📱 Responsive design
- 🚀 Fast static site generation
- 🎨 Bloqr's Filter design system — dark-first, with a light-mode adaptation
- 🌙 **Dark mode** with theme toggle and localStorage persistence
- 🔎 **Client-side search** across all documentation
- 🔗 Verified internal links (checked by `npm run build`, not just eyeballed)

## Deployment

The site is automatically built and deployed to GitHub Pages when changes are pushed to the main branch (`.github/workflows/gatsby.yml`).

Site URL: https://bloqrai.github.io/bloqr-core

## Future extraction plan

Per the parent repo's long-term plan, this site is expected to eventually become its own repository (the same path `bloqr-apiclients` and `bloqr-blocklists` already took out of the original monorepo). Two things will need to change when that happens, both already called out where they're implemented:

1. **`docs/` sourcing** (`gatsby-config.js`): the site currently reads `bloqr-core`'s `docs/` folder via a relative `gatsby-source-filesystem` path (`${__dirname}/../docs`), since it's a sibling directory today. Once extracted, `docs/` won't be a sibling anymore — this needs to become a vendoring step instead (a git subtree/submodule pointed at `bloqr-core`, or a sync script that copies `docs/` in at build/CI time before `gatsby build` runs).
2. **`pathPrefix`** (`gatsby-config.js`): currently `/bloqr-core`, matching this repo's GitHub Pages subpath. A standalone site would likely serve from its own domain or a different subpath, so this becomes a one-line config change, not a re-architecture.

Nothing else in this package assumes it lives inside the `bloqr-core` monorepo — pages, components, styles, and brand assets are all self-contained under `website/`.
