# AGENTS.md

Standing instructions for AI agents in this repository. `CLAUDE.md` imports this file.

## Autonomous Operating Policy (standing, from the owner, 2026-10-22)

This section is the owner's standing authorization and process for every BloqrAI repository. Read it at the start of every session and follow it without being reminded. For everything it authorizes, do not ask permission ("can I merge?", "can I write this key?"): just do it. It governs process and authority; the code, security and style rules elsewhere in this repository still apply, and where two rules conflict the stricter one wins. The same text lives in each BloqrAI repository's `AGENTS.md`; when you change it, change every copy.

### When it applies

- Autonomous backlog work starts only when the owner asks (for example "go work on small backlog issues that don't need back and forth", or "autonomous backlog mode"). Never start it unprompted. When asked, keep working eligible issues until none remain or the owner says stop.
- Work strictly serially: one BLO actively in work at a time, from In Progress through Merged, before starting the next. An issue waiting in Merged for the CI run on `main` is not active: start the next while it waits, and move it to Done when its run is green. Never work issues in parallel and never spawn background agents or parallel branches for backlog work (it was tried once and the cleanup cost more than working serially).
- Ignore PRs that neither you nor the owner created (Dependabot and other automation). Do not review, comment on, update or merge them.

### Eligibility

- Estimates are Fibonacci, 13 is the cap (extended estimates are off). Size like a competent human engineer would: complexity, uncertainty and blast radius. Do not size by AI time, and never count waiting time (CI, review, merges).
- **5 SP or fewer:** take it start to finish with no human involvement. **More than 5:** the owner participates; label `needs-human` and move on.
- Re-estimate after reading the issue. If it is larger or less clear than its estimate, change the estimate, label `needs-human`, leave a two-line comment saying why, and pick the next issue.
- `needs-human` at any size: auth; billing; a requirement that needs back and forth; any `one-way` change (irreversible, see below) except a dev migration that meets the conditions below; a change CI cannot cover (code with no test you can add, Cloudflare binding or `wrangler` config behavior, behavior against real third-party APIs, visual changes without visual tests, timing or races); a change to the release gate itself (the CI jobs that gate release or deploy, their `if:` conditions, `permissions:`, or secret handling).
- Other workflow edits are allowed.
- **Data migrations** are allowed against the dev (not live) Neon project when all three hold: the change is additive (new tables, columns, indexes); you take a Neon snapshot or branch first; you run it on a branch, verify, then apply it. Drops, type changes and table rewrites are `one-way`. This flips to "always `needs-human`" once the product is live.
- Labels (create them if missing): `claude` (every issue you work), `needs-human`, `needs-secret`, `one-way`. Keep these names neutral: they can appear in dashboards.

### Lifecycle of one issue (work it like a human; every transition happens in Linear)

1. Look for an existing PR for the issue that was never linked (search PRs for the BLO id in title, branch and body). If one exists, link it to the issue and advance the status to match its real state.
2. Assign to the owner, add `claude`, move to In Progress.
3. Set the estimate, type labels and priority. Leave the cycle empty until the owner says a sprint exists. Ignore other fields.
4. Branch from the latest `origin/main`, one branch per issue, named from the issue's Linear branch name. If the session fixes a branch name, reuse it serially and reset it onto fresh `origin/main` after each merge (never stack on merged history).
5. Do the work. Every change ships with tests where the repository has a test setup. Run the repository's own fast checks (lint, format, typecheck, tests; see its README or CLAUDE.md for the commands) before pushing.
6. Open the PR as a draft, filling in the repository's PR template if it has one. The title is a Conventional Commit (it becomes the squash commit message and can drive version bumps). The body contains `Fixes BLO-n`. After opening, confirm the PR shows on the Linear issue; if it does not, attach it with the Linear attachment tool. A PR that is not linked to its issue is a defect.
7. When CI is green (or immediately, if the repository has no CI or its CI is unavailable), mark the PR ready for review. Copilot reviews once per PR (draft and new-push review are off). Move the issue to In Review.
8. Fix every valid Copilot finding, reply with the reason for any you decline, resolve every thread, and keep CI green. If no Copilot review appears, request one once with the review tool; if still none, merge on green and note it on the issue.
9. Merge when CI is green on the head commit, every thread is resolved and there is no conflict. Squash only. No human approval is required or sought, except for a PR that changes this policy (see "This policy is the owner's" below). Move the issue to Merged.
10. After merging, find the workflow run(s) for the merge commit on `main`. If the repository has a release or deploy gate (in `bloqr-compiler`, the `ci-gate` then `deploy` jobs of `ci.yml`, which is the only path to Cloudflare), the issue is Done only when that run is green. If the repository has no CI, or its CI is unavailable, move the issue to Done once merged and say so in the batch summary. If a run is red, leave the issue in Merged and fix forward or open a revert PR, whichever is faster and safer. A red `main` never stops new work: keep picking up issues and let work pile up, or fix `main` first when your own merge broke it (new branches inherit the failure, so fixing it first is usually the shortest path).

Linear's GitHub integration may also move statuses on PR events; set statuses explicitly and reconcile any difference. Merge conflicts are yours to resolve: merge `main` into the branch, regenerate lock files with the tooling (never by hand), re-run checks, push. Almost all conflicts come from bot PRs that landed after you branched.

### Secrets and 1Password

- **1Password is the source of truth for the keys you handle.** It replaces environment-variable secrets in this agent environment; the single exception is the bootstrap `OP_SERVICE_ACCOUNT_TOKEN`. Do not ask the owner to put any other key in the environment. This does not change how applications consume secrets: runtime secrets stay where this repository's own rules put them (Worker Secrets set with `wrangler secret put`, `.dev.vars` locally, GitHub Actions secrets). Read the value from 1Password and set it there; never use the dashboard where this repository forbids it.
- **Scope:** use only the `Claude-*` vaults (`Claude-Dev` now; `Claude-Prod` later with its own service account). Never list, read or write any other vault (Engineering, Finance, Root and Recovery, personal or employee vaults). If the service account can see another vault, stop and tell the owner.
- **The `op` CLI** must be a verified binary. Best case, the environment's setup script installs it. If `op` is missing in a fresh container, download the version-pinned zip (currently 2.30.0) from `cache.agilebits.com` into a temp directory, import 1Password's signing key from `downloads.1password.com/linux/keys/1password.asc` into a throwaway `GNUPGHOME`, confirm its fingerprint is `3FEF9748469ADBE15DA7CA80AC2D62742012EA22` (compare with 1Password's CLI documentation), and run `gpg --verify op.sig op` using the `op.sig` inside the zip. Do not run the binary, and never give it the service account token, until the signature verifies. If verification fails or cannot be done, do not use the binary: mark the work `needs-secret` and flag it.
- **Standing permission, no asking:** read keys from 1Password, write and update keys in 1Password, mint keys through any connector that allows it (immediately store the new key in 1Password), set Cloudflare Worker secrets, and use a key right after reading it.
- **Minting limits:** mint only the least-privilege, purpose-bound credential the issue needs (narrowest scopes, shortest practical lifetime, named for its purpose), store it in the vault for its environment, and never create, overwrite or rotate a credential for an environment the owner has declared live (the owner keeps the list under "Live environments" below; if an environment is not clearly classified there, treat it as live). Credentials for a live environment are `needs-human`.
- **Never reveal a value.** Pipe `op read` straight into the one command that needs it, or use `op run`; never run `op read` on its own, because its output would reach the terminal and tool logs, and never echo, log or write a value to disk. Never put a value in Linear, GitHub, a PR, a commit, a comment or chat. Refer to a key only by its item name.
- **Naming and tags:** item title `<system>/<purpose>/<env>` (for example `cloudflare/worker-deploy/dev`). Tags: `owner:claude`, `env:<dev|prod>`, `minted-by:claude` when you minted it, `created-for:BLO-n`, `rotate-by:<date>`. Notes: where the key is used and its scopes.
- **GitHub Actions secrets** are the one manual step. If `github/actions-secrets-writer/dev` exists in `Claude-Dev`, use it to set Actions secrets through the REST API (sealed-box encrypted); otherwise follow the `needs-secret` flow below.
- **`needs-secret` flow:** a missing key never stops your work. Push the issue as far as it can go (PR open, Copilot review done, every thread resolved), hold the merge if CI needs the key, add `needs-secret`, and comment the exact secret names, where each goes, and a ready-to-paste brief for the owner. The owner supplies keys in one batch at the end of the day, re-runs the failed jobs, and the green run wakes you to merge. Do not add skip logic that lets a job pass without its secret.
- If a connector can mint a key but you lack the permission, treat that as a missing key (`needs-secret`), not a blocker.

### Live environments (owner-maintained)

None are live yet (owner, 2026-10-22). Resources with a production-style name are pre-launch until the owner moves them to this list: this includes a Neon `production` branch and any Grafana stack that Git Sync keeps current from `main`. The owner updates this list when something goes live; an agent never edits it. If you cannot tell whether an environment is live, treat it as live.

### Standing authorizations and hard stops

- Authorized without asking: create branches and PRs, merge your own PRs under the rules above, fix CI on `main`, resolve merge conflicts, create and edit labels, update Linear (status, estimates, labels, priority, assignee, links), run dev migrations under the conditions above, edit workflows except the release gate, mint and store keys, delete branches and worktrees you created.
- **This policy is the owner's.** Change it only at the owner's explicit instruction, and the owner merges any PR that changes it. Never merge such a PR yourself, even in a backlog run.
- Always flag instead of acting: auth; billing; `one-way` changes outside the dev-migration case; anything over 5 SP; exposing a secret; spending money or changing a plan; deleting anything other than branches and worktrees you created (repos, DNS, data, secrets, infrastructure, vault items, Linear issues), even when you created it. Flag by labeling `needs-human` and moving on.

### Communication

- **Autonomous backlog runs are silent by default.** The owner may remind you; if they say they are watching or ask for updates, narrate normally. In a silent run, do not narrate: no progress chatter and no per-ticket messages. Work the batch, then give one summary at the end: what merged, what is Merged but not yet Done, every `needs-human` and `needs-secret` item with its reason, and anything red. For a long batch also write the same summary as a Markdown file (not a Google Doc) in a `Claude Run Reports` folder in the owner's Google Drive, creating the folder if it is missing, and link it; if Drive is unavailable, the final message alone is the summary. Stop mid-batch only for a hard stop above. The owner then reviews, chats through any issues, and starts the next cycle.
- Outside a backlog run, give normal working updates (the owner often watches), terse in wording. Put anything the owner must do in one block headed "For your scratchpad", with no urgency and no deadlines. Do not end replies with a list of what you are waiting on. The owner batches context switches, so never ask for something one item at a time.
