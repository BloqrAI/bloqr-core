// One-off generator for the static Open Graph / Twitter card preview image.
// Run with `node scripts/generate-social-preview.mjs` after editing the SVG
// below, then commit the resulting `static/social-preview.png`. Not part of
// the Gatsby build itself — sharp is a devDependency only for this script.
import sharp from "sharp"
import { fileURLToPath } from "node:url"
import path from "node:path"

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const outFile = path.join(__dirname, "..", "static", "social-preview.png")

const width = 1200
const height = 630

const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#070B14"/>
      <stop offset="100%" stop-color="#0C1424"/>
    </linearGradient>
    <radialGradient id="glow" cx="80%" cy="10%" r="70%">
      <stop offset="0%" stop-color="#00D4FF" stop-opacity="0.18"/>
      <stop offset="100%" stop-color="#00D4FF" stop-opacity="0"/>
    </radialGradient>
  </defs>

  <rect width="${width}" height="${height}" fill="url(#bg)"/>
  <rect width="${width}" height="${height}" fill="url(#glow)"/>

  <g transform="translate(90, 210)">
    <rect x="0" y="10" width="140" height="26" rx="13" fill="#F1F5F9"/>
    <rect x="0" y="58" width="100" height="26" rx="13" fill="#00D4FF"/>
    <rect x="0" y="106" width="60" height="26" rx="13" fill="#FF5500"/>
  </g>

  <text x="260" y="300" font-family="'Space Grotesk', 'Segoe UI', system-ui, sans-serif" font-size="88" font-weight="700" letter-spacing="0.01em" fill="#F1F5F9">Bloqr Core</text>
  <text x="262" y="360" font-family="'Space Grotesk', 'Segoe UI', system-ui, sans-serif" font-size="26" font-weight="600" letter-spacing="0.22em" fill="#94A3B8">INTERNET HYGIENE</text>

  <text x="90" y="450" font-family="'Segoe UI', system-ui, sans-serif" font-size="30" font-weight="400" fill="#CBD5E1">
    Compile, validate, and manage AdGuard-syntax
  </text>
  <text x="90" y="492" font-family="'Segoe UI', system-ui, sans-serif" font-size="30" font-weight="400" fill="#CBD5E1">
    ad-blocking filter lists across six languages.
  </text>

  <text x="90" y="562" font-family="'Segoe UI', system-ui, sans-serif" font-size="24" font-weight="600" fill="#FF5500">core.bloqr.dev</text>
</svg>
`

await sharp(Buffer.from(svg)).png().toFile(outFile)
console.log(`Wrote ${outFile}`)
