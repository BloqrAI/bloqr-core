# DRAFT — Bloqr Core License (Source-Available, Non-Commercial)

**Status: DRAFT — NOT YET ADOPTED.** This is a candidate replacement for the
root `LICENSE` file, written for review by the repo owner and, before it
ships, by counsel. It is not in effect. The repository's actual license
today remains whatever `LICENSE` currently says, notwithstanding the
inconsistent GPL-3.0 references elsewhere in the tree (tracked separately —
see the licensing-strategy doc and its tracking issue).

This draft is modeled on [`bloqr-compiler`'s existing source-available
license](https://github.com/BloqrAI/bloqr-compiler/blob/main/LICENSE) so the
FOSS and commercial sides of the org read as one consistent family, adapted
for the fact that `bloqr-core` ships as source code people run themselves,
not a hosted/metered service — so the SaaS-API-billing and proxy-metering
license paths that make sense for `bloqr-compiler` don't apply here and have
been dropped in favor of a single "contact Bloqr for commercial terms" path.

Open questions for the repo owner / counsel, called out inline below with
`[OPEN QUESTION]` markers, plus at the end.

---

## Bloqr Core License

Version 1.0 (DRAFT)

Copyright (c) 2026 Bloqr Systems, Inc. All rights reserved.

### TERMS AND CONDITIONS

This software ("Software") is made available under the following terms:

### 1. NON-COMMERCIAL USE (FREE)

You may use, copy, modify, and distribute the Software freely for
non-commercial purposes, including but not limited to:

- Personal projects
- Educational purposes
- Open-source projects (that do not generate revenue)
- Internal organizational use by non-profit organizations
- Research and development
- Operating your own ad-blocking filter lists, hostlists, or similar
  artifacts and distributing them **at no charge**

This license grants you the right to view, study, and understand the
source code. All modifications must be made in a private, non-distributed
manner or contributed back to the official Bloqr repository via pull
request.

**Using the Software to produce an artifact (a compiled filter list, a
configuration, generated output of any kind) that you give away for free is
non-commercial use, even if you are a business or professional entity,**
provided the artifact itself is not sold, is not paywalled, and is not used
to directly generate revenue (e.g., bundled into a paid product or service,
gated behind a subscription, or monetized via a fee for access).

### 2. COMMERCIAL USE (RESTRICTED)

If you or your organization uses the Software, or any artifact produced by
the Software, to generate revenue or for any commercial purpose, you must
obtain a commercial license from Bloqr Systems.

"Commercial use" includes, but is not limited to:

- Using the Software as part of a product or service sold, licensed, or
  offered to third parties for a fee
- Selling, sublicensing, or paywalling any artifact produced by the
  Software (a compiled filter list, hostlist, or similar output)
- Using the Software to generate revenue through any monetization model
  (subscriptions, advertising revenue directly tied to the artifact,
  paid support built around the artifact, etc.)
- Using the Software in a business context to create competitive advantage
  where that advantage is monetized
- Offering services that derive revenue from the Software or its output

Bloqr Systems, Inc. and its employees and contractors acting on its behalf
retain unlimited rights to use the Software for any purpose, commercial or
otherwise, without restriction.

Third parties may obtain a commercial license from Bloqr Systems to use the
Software (or artifacts it produces) commercially. See
[`bloqr-compiler`'s Commercial License Agreement
template](https://github.com/BloqrAI/bloqr-compiler/blob/main/COMMERCIAL_LICENSE.md)
for the shape such an agreement takes for Bloqr's commercial products — the
equivalent agreement for `bloqr-core` would follow the same pattern, minus
the SaaS-API/proxy-metering terms that are specific to `bloqr-compiler`
being a hosted service.

To inquire about commercial licensing, contact: sales@bloqr.dev

`[OPEN QUESTION]` Should "artifact produced by the Software" commercial use
be scoped identically to how `bloqr-compiler`'s license treats it, or does
`bloqr-core`'s much lower-friction, self-hosted, no-API-metering nature
warrant a narrower or differently-worded definition? The two products have
different practical enforcement postures — `bloqr-compiler` can meter usage
through its own infrastructure; `bloqr-core` cannot observe downstream use
at all once someone has the source. Worth deciding whether that changes what
the license should promise to actually be able to enforce, versus what it
states as the honor-system expectation.

### 3. RESTRICTIONS

Regardless of use case, you may NOT:

- Modify, reverse-engineer, or create derivative works for redistribution
  outside your organization without permission
- Republish the Software under a different package name, organization, or
  branding
- Create a competitive product using the Software as a base
- Remove, obscure, or alter this license or any copyright notices
- Use the Software to provide a functionally similar competing service

### 4. NO MODIFICATIONS FOR DISTRIBUTION

All modifications must remain private to your organization unless
contributed back to the official Bloqr repository via pull request. Public
distribution of modified versions outside that path is forbidden.

### 5. REDISTRIBUTION RIGHTS

- You may redistribute the unmodified Software with its original source
  code, provided you include this license and all copyright notices.
- For modified versions: redistributing modified versions outside your
  organization requires explicit written permission from Bloqr Systems.

### 6. OPEN SOURCE COMPATIBILITY

This Software uses dependencies under various open-source licenses (MIT,
Apache 2.0, etc. — see the dependency license compatibility report tracked
separately). Using the Software under non-commercial terms does not require
you to open-source your own applications. When using the Software
commercially under a Bloqr commercial license, you remain free to keep your
own applications proprietary.

`[OPEN QUESTION]` This clause is only true once the dependency audit (see
the tracking issue) confirms no copyleft (GPL/AGPL/LGPL/MPL) dependency has
snuck in anywhere across the six language ecosystems this repo spans. Do not
publish this license until that audit is clean, or until any copyleft
dependency found is removed/replaced.

### 7. DISCLAIMER OF WARRANTY

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, AND NONINFRINGEMENT. IN NO EVENT SHALL
BLOQR SYSTEMS BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT, OR OTHERWISE, ARISING FROM, OUT OF, OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

### 8. GOVERNING LAW

`[OPEN QUESTION]` — needs the actual jurisdiction Bloqr Systems is
domiciled in; left as a placeholder to match `bloqr-compiler`'s license,
which has the same open item.

This license is governed by the laws of the jurisdiction where Bloqr
Systems is domiciled, without regard to its conflict of law provisions.

### 9. CHANGES TO THIS LICENSE

Bloqr Systems reserves the right to change these license terms for future
versions of the Software. Changes will not retroactively apply to prior
versions you are already using under previous license terms, but new
versions must be used under updated terms.

---

For questions about this license, contact: legal@bloqr.dev
For commercial licensing inquiries, contact: sales@bloqr.dev

---

## Open questions for the repo owner / counsel

1. **Published-package interaction.** `bloqr-core` already publishes real
   packages under permissive terms today via their own registries'
   metadata: `@bloqr/compiler-core` (JSR), `bloqr-validator-core` /
   `bloqr-validator-core-cli` / `bloqr-compiler` (crates.io),
   `Bloqr.Compiler.Abstractions` / `Bloqr.Compiler.Core` (NuGet via GitHub
   Packages). Each registry's package metadata currently says GPL-3.0.
   Switching the repo's license changes what those packages should declare
   going forward — but already-published versions are immutable on their
   registries and will keep showing GPL-3.0 forever. Decide: is a
   version bump + republish under the new license terms required for each,
   and on what timeline relative to the repo-wide license swap?
2. **SPDX identifier.** This draft's license is not a standard SPDX-listed
   identifier (it's the same custom category as `bloqr-compiler`'s). Some
   package registries want an SPDX identifier or a specific string; NuGet,
   crates.io, and JSR all support a `LICENSE-FILE`-only path (no SPDX
   requirement), so this shouldn't block anything, but worth confirming
   per-registry before the swap.
3. **Existing external forks/clones.** Anyone who already cloned/forked
   `bloqr-core` under the current MIT-claiming `LICENSE` file arguably has
   a colorable claim to keep using that snapshot under MIT terms
   (depending on jurisdiction and how look-back relicensing is typically
   treated) — worth a short note in the eventual `CHANGELOG`/release notes
   marking the exact commit/date the license changed, so there's a clean
   line between "MIT-era" and "Bloqr-license-era" history.
4. **Enforcement posture.** See the `[OPEN QUESTION]` under Section 2 above
   — `bloqr-core` has no built-in usage metering the way `bloqr-compiler`
   does, so this license is closer to an honor-system + trademark/branding
   enforcement model than a technically-enforced one. Confirm that's an
   accepted tradeoff.
