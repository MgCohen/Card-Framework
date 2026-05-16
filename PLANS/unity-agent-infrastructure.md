---
type: plan
status: draft
tags: [#infra, #claude-code, #unity, #agents, #remote]
---

# Unity Agent Infrastructure — implementation plan

> **Goal.** Stand up a single always-on Linux VM that hosts Unity (headless) and multiple git worktrees of this repo, so Claude Code can drive Unity-aware agent work on N branches in parallel — both as **remote chat sessions** I open from anywhere, and as **dispatched agent runs** triggered by webhook/REST. Replaces the current "leave my desktop on 24/7 and RDP in" workflow.

---

## 1. What we're building

Two complementary surfaces on one shared VM:

- **Remote Chat**: a browser-accessible Claude Code TUI running inside a git worktree on the VM. Drop in from phone or laptop, work normally, leave the session running, come back. Same `claude` you'd run locally — just persistent and reachable.
- **Agent Dispatcher**: a small service on the same VM that accepts task requests (REST or GitHub webhook), picks a free worktree slot, spawns `claude` against it with the task prompt, streams logs back, and pushes a branch + opens a PR when done.

Both share the same Unity install, license, Library cache, and `claude` subscription auth on the same box. The dispatcher is just "automated typing into the same TUI Remote Chat exposes interactively."

---

## 2. Final shape

```
┌─────────────────────────────────────────────────────────────┐
│                  External (you, GitHub)                     │
└───────┬───────────────────────┬──────────────────┬──────────┘
        │ browser/SSH           │ POST /tasks      │ webhook
        │ via Tailscale         │ (laptop, phone)  │ (PR comment)
        ▼                       ▼                  ▼
   ┌─────────┐            ┌──────────────────────────────┐
   │  ttyd   │            │   FastAPI dispatcher (8080)  │
   │  :7681  │            │   - REST + WebSocket logs    │
   └────┬────┘            │   - Asyncio queue + slots    │
        │                 └──────────────┬───────────────┘
        ▼                                ▼
   tmux(chat) ────► claude TUI    spawn claude (PTY or -p)
                                            │
                       ┌────────────────────┼────────────────────┐
                       ▼                    ▼                    ▼
                ~/work/main          ~/work/slot1         ~/work/slot2
                (interactive)        (agent task)         (agent task)
                       │                    │                    │
                       └────────────┬───────┴───────────┬────────┘
                                    ▼                   ▼
                         Unity -batchmode (per worktree, own Library/)
                                    │
                                    ▼
                            Unity Accelerator (shared cache)
```

**Hardware**: one Hetzner CCX33 (8 vCPU / 32 GB / 240 GB NVMe), Ubuntu 24.04, behind Tailscale.

**Auth model**: `claude` is authenticated against my Max subscription via `CLAUDE_CODE_OAUTH_TOKEN`. `ANTHROPIC_API_KEY` is **unset** everywhere on the VM. Dispatcher spawns the official CLI as subprocesses, so dispatched runs ride the same auth as interactive chat.

**Concurrency**: 2 agent slots initially (`slot1`, `slot2`), plus the interactive `main` worktree. Easy to grow to 4–5 if the box can handle it.

---

## 3. What we can do once it's built

- Open a browser tab on phone → land in a live `claude` session inside the Unity project. Run tests, write code, iterate. Disconnect. Reconnect later, session is still there.
- `POST /tasks {"branch": "feat/new-card-effect", "prompt": "implement X using the design in DOCS/Y.md"}` from anywhere → VM runs the task in a free worktree, commits, pushes, opens a PR. Stream live logs into a web UI or tail them from the terminal.
- Comment `@bot implement issue #123` on a GitHub PR/issue → webhook hits the dispatcher → same as above.
- Run 2–3 Unity batch-mode tasks in parallel without re-importing the asset database each time (warm Library per worktree, Unity Accelerator backing them).
- Keep working when my desktop is off, asleep, or disconnected — the VM is the single source of truth for in-flight agent work.

**Non-goals** (explicitly out of scope for v1):
- No multi-VM / horizontal scaling. One box.
- No GPU / no Editor with rendering. Batch mode only — visual verification still happens on my local Unity install against the branch the agent pushed.
- No durable workflow engine (no Temporal/Restate). In-memory queue + SQLite is enough for solo use.
- No custom agent framework (no Flue, no Agent SDK as driver). Official `claude` CLI is the only thing talking to Anthropic — keeps subscription billing intact.

---

## 4. Services & accounts needed

| Service | Purpose | Required? |
|---|---|---|
| **Hetzner Cloud** | The VM itself (CCX33) | Required |
| **Tailscale** | Private network, no public ports | Required |
| **Claude Max** subscription | Already have. The `claude` CLI bills against this. | Have it |
| **Unity Personal license** | Free if revenue cap qualifies. Verify EULA. | Required |
| **GitHub PAT** (fine-grained, repo scope) | Dispatcher pushes branches + opens PRs | Required |
| **Domain + Cloudflare DNS** | Nicer URLs for ttyd / dispatcher | Optional |
| **Caddy** | TLS + reverse proxy on the VM | Optional but recommended |

---

## 5. Costs

| Item | Monthly | Notes |
|---|---|---|
| Hetzner CCX33 | ~€57 (~$62) | 8 vCPU / 32 GB / 240 GB. Can start on CCX23 (~€29) if budget-tight. |
| Hetzner backups (20%) | ~€11 | Optional — recommend on at first, can drop once stable. |
| Tailscale | $0 | Personal plan covers this. |
| Claude Max | (already paid) | 5x or 20x — already on it. |
| Unity Personal | $0 | Free if I qualify under the revenue cap. |
| Domain | ~$1/mo amortized | Optional. |
| **Total new spend** | **~$60–75/mo** | One line item on the credit card. |

**Anthropic billing risk (the June 15, 2026 thing)**:
- Interactive `claude` TUI stays on Max quota — both Remote Chat and Path-A dispatcher rely on that.
- `claude -p` / headless / Agent SDK paths move to a separate Agent SDK credit pool at API rates on June 15. If we use Path B (`-p`-based dispatcher) we'll see API-priced charges for agent runs after that date. Budget zero today; revisit once we have task-volume data.

---

## 6. Implementation plan — phased

Each phase is a usable stopping point. Don't start phase N+1 until phase N is solid.

### Phase 0 — Decisions to lock first (do before provisioning)

- [ ] Confirm Unity Personal EULA qualification for our entity/revenue situation. If we don't qualify, this whole plan needs a re-cost with Unity Pro (~$2,200/yr/seat).
- [ ] Confirm comfort with "Max subscription on a personal VM" interpretation. (Anthropic permits personal-device use; a VM I own falls under that. Document the decision.)
- [ ] Pick dispatcher path: **Path A (PTY-driven TUI)** = stays on subscription, more code complexity. **Path B (`claude -p`)** = simpler, API-priced after June 15. Recommendation: ship Path B first to prove the loop, switch to Path A only if API cost becomes real.
- [ ] Choose concurrency target: 2 slots (CCX33) vs. 4 slots (CCX43 or higher).

### Phase 1 — VM foundation (½ day)

- [ ] Hetzner: create CCX33, Ubuntu 24.04, add SSH key, attach a 100 GB volume if we want Library/ on a separate disk.
- [ ] Initial hardening: non-root user, `ufw` allow 22 only, unattended-upgrades on, fail2ban.
- [ ] Tailscale: `tailscale up` on VM and laptop/phone. Confirm SSH over Tailscale works.
- [ ] Install base tooling: `git`, `tmux`, `build-essential`, `python3.12`, `pipx`, `nodejs` (for any tooling that needs it), `gh`-less env (we use Octokit/REST direct).

### Phase 2 — Unity headless (½ day)

- [ ] Install Unity Hub headless: download `.AppImage` or apt repo, install required libs.
- [ ] Install the Editor version `ProjectSettings/ProjectVersion.txt` requires.
- [ ] Activate license:
  ```bash
  Unity -batchmode -createManualActivationFile -logFile -
  # upload .alf at https://license.unity.com → download .ulf
  Unity -batchmode -manualLicenseFile <file>.ulf -logFile -
  ```
- [ ] Install Unity Accelerator (separate package, runs as a service). Point at it from project settings.
- [ ] Smoke test: clone repo to `~/work/main`, run `Unity -batchmode -nographics -quit -projectPath ~/work/main -logFile -`. Confirm clean import + exit code 0.
- [ ] **Disable Parallel Import for headless** (known Linux bug): set in `ProjectSettings/EditorSettings.asset` or pass `-DisableDirectoryMonitor`.

### Phase 3 — Worktrees + Claude Code auth (¼ day)

- [ ] `git worktree add ~/work/slot1 <some-branch>` and `~/work/slot2`. Verify each gets its own Library/.
- [ ] On laptop: `claude setup-token` → produces 1-year OAuth token.
- [ ] On VM: write `CLAUDE_CODE_OAUTH_TOKEN=...` into `/etc/profile.d/claude.sh` (or per-user `.env`).
- [ ] **Audit**: `env | grep -i anthropic` on VM → must show only the OAuth token, *no* `ANTHROPIC_API_KEY`.
- [ ] Install Claude Code on VM, run `claude` in `~/work/main`, hit `/status` — confirm it shows the Max plan.
- [ ] Test a trivial task end-to-end in the TUI: "list the files in Assets/Scripts and exit". Confirm it works.

### Phase 4 — Remote Chat (1 evening)

- [ ] Install `tmux` and `ttyd`.
- [ ] Launcher script `~/bin/start-chat.sh`:
  ```bash
  #!/usr/bin/env bash
  exec tmux new-session -A -s chat -c ~/work/main claude
  ```
- [ ] systemd user unit `ttyd-chat.service` that runs `ttyd -p 7681 -W ~/bin/start-chat.sh` (or use `-c user:pass` if not behind Tailscale-only).
- [ ] Bind ttyd to the Tailscale interface only: `ttyd -i tailscale0 -p 7681 ...`.
- [ ] (Optional) Caddy in front for `https://chat.<domain>` with TLS and basic auth.
- [ ] Test from phone: open the URL → see live `claude` TUI → type, get a response, detach, reconnect.

### Phase 5 — Dispatcher v1, Path B (1–2 days)

Path B = `claude -p` (simpler, API-priced post–June 15). Use this to prove the architecture before deciding on Path A.

- [ ] `/srv/dispatcher/` — FastAPI app, single `main.py`:
  - `POST /tasks {branch, prompt}` → enqueues, returns `task_id`
  - `GET /tasks` → list (status, branch, slot, started_at)
  - `GET /tasks/{id}/logs` → WebSocket streaming
  - SQLite for task state, asyncio queue + semaphore for slots.
- [ ] Worker coroutine per task:
  1. Acquire slot from semaphore (size = N).
  2. `cd ~/work/slot{N}` → `git fetch origin && git checkout <branch> && git reset --hard origin/<branch>`.
  3. Run `claude -p "$prompt" --dangerously-skip-permissions` (or with a curated `--allowedTools`). Stream stdout/stderr to log file + WebSocket subscribers.
  4. On exit: check for changes, `git add -A && git commit -m "..." && git push -u origin <branch>`.
  5. Open PR via GitHub REST (PAT in env).
  6. Release slot.
- [ ] systemd unit `dispatcher.service`, restart on failure, logs to journald.
- [ ] Bind to Tailscale interface only.
- [ ] Smoke test: `curl -X POST .../tasks -d '{"branch": "test/dispatcher-smoke", "prompt": "add a comment to README.md"}'` → see PR appear.

### Phase 6 — GitHub webhook trigger (½ day, optional)

- [ ] Add `POST /webhook/github` to the dispatcher, verify HMAC signature against `GITHUB_WEBHOOK_SECRET`.
- [ ] Parse `issue_comment.created` events; trigger when body matches `@bot implement <prompt>`.
- [ ] Register webhook on the repo, scoped to `issue_comment` + `pull_request_review_comment` events.
- [ ] Test by commenting on a PR.

### Phase 7 — Minimal web UI for the dispatcher (½ day, optional)

- [ ] One-page htmx (or plain HTML + vanilla JS) at `/` listing tasks.
- [ ] Click a task → opens WebSocket log stream in a `<pre>`.
- [ ] Button to cancel a running task (sends SIGTERM to the worker subprocess).

### Phase 8 — Path A migration, *if needed* (1–2 days)

Only do this if dispatched-run volume × API rates becomes a real cost after June 15.

- [ ] Add `pexpect` (Python) or `node-pty` worker mode behind a feature flag.
- [ ] Worker spawns `claude` interactively under PTY, writes the prompt to stdin, reads stdout until a sentinel (e.g., ask Claude to write `.task-done` when finished).
- [ ] Capture pane output to log file with ANSI stripped for the WebSocket stream.
- [ ] Verify billing in Anthropic console shows subscription usage, not API usage, after a dispatched run.

---

## 7. Operational notes

- **Backups**: enable Hetzner snapshots weekly. Library/ doesn't need backup (regenerable). `~/work/*/.git` is fine because `origin` is the source of truth. The thing actually worth backing up is the **Unity license file** (`~/.local/share/unity3d/Unity/Unity_lic.ulf`) and the dispatcher's SQLite + config.
- **Monitoring**: `journalctl -u dispatcher -f` for now. Add a simple Healthchecks.io ping from the dispatcher heartbeat if/when needed.
- **Updates**: monthly cadence — `apt upgrade`, `claude` self-update, Unity Editor minor versions only when our project bumps `ProjectVersion.txt`.
- **Disk pressure**: each worktree's Library/ is 5–20 GB. With 3 worktrees + Accelerator cache + system, plan ~80–120 GB used. CCX33's 240 GB is comfortable; CCX23's 80 GB is tight.

---

## 8. Risks & landmines

- **June 15, 2026 billing split** — `claude -p` moves off Max quota. Mitigations: Path A (PTY) ready as fallback; track dispatched-run volume; budget API spend.
- **Unity EULA on Personal for headless/CI** — Personal is permitted for individual use but the "one machine" clause is fuzzy when "the machine" is a VM. If revenue cap is borderline, we need Pro.
- **Anthropic ToS narrowing further** — Feb 2026 ban on Agent SDK with OAuth tokens already happened. The PTY-driving-the-TUI pattern (Path A) is not what Anthropic designed for; they could harden against it. If that happens, fallback is API-billed dispatcher or a different model provider.
- **Unity headless Parallel Import bug** — known and mitigated by disabling Parallel Import. Re-test on each Editor version bump.
- **Single point of failure** — one VM. If Hetzner has an outage, everything stops. Acceptable for solo dev; revisit if it becomes load-bearing.

---

## 9. Open questions / TBD

- [ ] Path A vs. Path B as the v1 dispatcher? (Recommendation: B first, A if/when API cost bites.)
- [ ] Chat UI: ttyd-only, or also stand up siteboon/claudecodeui for a nicer experience? (Recommendation: ttyd first — minimal surface area, guaranteed subscription-billed.)
- [ ] Webhook trigger now (Phase 6) or later? (Depends on how often we want PR-driven runs vs. laptop-driven.)
- [ ] Do we want a "watchdog" Claude on the VM that monitors PR CI and auto-fixes? Out of scope for v1, but the infra would support it.
- [ ] Library/ on the root disk vs. a separate Hetzner Volume? (Volume is cheaper per GB and detachable for backup, but slightly slower I/O.)

---

## 10. References

- Unity + worktrees research → see conversation 2026-05-16 (Hetzner CCX, Accelerator, Parallel Import bug)
- Claude Code subscription on VM → `claude setup-token`, OAuth flow, `ANTHROPIC_API_KEY` precedence trap
- Dispatch frameworks survey → FastAPI+RQ vs. Trigger.dev/Inngest; Flue ruled out (would force API billing)
- Anthropic policy timeline → Feb 2026 SDK token ban; June 15, 2026 `-p`/SDK credit-pool split
