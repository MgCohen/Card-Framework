# Complete Course: Claude Code Subagents and Custom Agents

## 1. What Are They?

### Subagents (the runtime concept)
A **subagent** is a specialized Claude instance that the main Claude session spawns to handle a specific task. It runs with:
- **Its own context window** (separate from your main conversation)
- **Its own system prompt** (focused on a specialty)
- **Its own tool allowlist** (often restricted)
- **Its own model** (e.g. Haiku for speed, Opus for reasoning)

When done, it returns a **single summary message** to the main conversation. Its file reads, search results, and intermediate reasoning never enter your main context.

### Custom Agents (the configuration concept)
A **custom agent** is a markdown file you author that defines a reusable subagent. They live in:
- `~/.claude/agents/` — user-level (available in every project)
- `.claude/agents/` — project-level (checked into the repo)

Each file is YAML frontmatter + a markdown system prompt.

| | Subagent | Custom Agent |
|---|---|---|
| What | Runtime instance | Config file |
| Lifetime | Temporary | Persistent |
| Where | In memory | `.md` on disk |

---

## 2. How to Use Them

### Built-in agents (always available)
- **Explore** — read-only codebase search (Haiku)
- **Plan** — design an implementation strategy before coding
- **general-purpose** — full-stack research + multi-step execution

### Creating a custom agent
Create `.claude/agents/<name>.md`:

```markdown
---
name: my-agent
description: When this agent should be invoked (Claude reads this to decide)
tools:
  - Read
  - Glob
  - Grep
model: claude-opus-4-5
---

# System prompt body

You are an expert at <your specialty>. When given a task, do <step 1>, then <step 2>…
```

> The angle-bracket placeholders above are just prose for you to fill in — there's no templating or variable substitution. Whatever you write in the markdown body becomes the agent's literal system prompt.

The **description** is the most important field — Claude uses it to auto-delegate matching tasks.

### System prompt vs. invocation prompt

A subagent sees **two** things on every call:

1. **System prompt** — the markdown body of the `.md` file. Set once, defines the agent's persistent role.
2. **Invocation prompt** — the task brief passed at call time (the `prompt` argument to the Agent tool). This is where you inject per-task data/context.

```
System prompt (in code-reviewer.md):
  "You are an expert reviewer. For each task, identify
   Critical/High/Medium issues with file:line citations…"

Invocation prompt (passed when calling the agent):
  "Review this diff for security issues:
   <paste diff here>
   Focus on auth changes in lines 40–80. Ignore test files."
```

You can pass file paths, snippets, URLs, prior decisions — anything the agent needs. The only constraint is the agent's tool allowlist (e.g. without `Read` it can't open files you reference, only reason about pasted content).

### Managing them
- `/agents` in a session — interactive UI to list, create, edit, run
- `claude agents` from the shell — list configured agents

### Invoking
- **Automatic:** Claude matches your prompt against agent descriptions and delegates
- **Explicit:** "Use the security-auditor agent to check src/auth.ts"
- **Parallel:** Ask for multiple agents in one message; the SDK runs them concurrently

---

## 3. Benefits

- **Context isolation** — long research/log-heavy tasks don't pollute your main conversation
- **Specialization** — bake an expert system prompt in once, reuse forever
- **Tool restriction** — give a reviewer read-only tools so it can't modify code
- **Model selection** — cheap Haiku for grep-style exploration, Opus for hard reasoning
- **Parallelism** — run reviewer + tester + auditor concurrently
- **Reusability** — one user-level agent works across all projects
- **Cleaner main session** — your top-level conversation stays focused on decisions

---

## 4. Example: a Code Reviewer Agent

`.claude/agents/code-reviewer.md`:

```markdown
---
name: code-reviewer
description: Expert code reviewer for security, performance, and quality. Invoke for detailed code analysis, PR-style reviews, or refactor suggestions.
tools:
  - Read
  - Glob
  - Grep
  - WebSearch
model: claude-opus-4-5
---

# Code Reviewer

You are an expert reviewer. For each task:

1. Read all related files for context
2. Identify issues across: security, performance, readability, error handling
3. Categorize each as Critical / High / Medium / Low
4. Propose a specific fix with a code example
5. Call out genuine strengths

## Output format

### Summary
1–2 sentences.

### Critical Issues
- **Title** — Problem / Risk / Fix (with snippet)

### High / Medium / Low
Same structure, scaled.

### Strengths
What's done well.

Be specific (cite file:line), constructive, and pragmatic.
```

Usage:
```
You: Review src/auth.ts for security issues
→ Claude matches description, spawns code-reviewer
→ It reads files in its own context, returns a structured review
→ Your main conversation only sees the summary
```

---

## 5. Snippets

### Minimal agent
```markdown
---
name: quick-tester
description: Run the test suite and summarize results
tools: [Bash]
---

Run all tests. Report pass/fail counts and the first 3 failures with stack traces.
```

### Read-only auditor with model override
```markdown
---
name: security-auditor
description: Audit code for security vulnerabilities — credentials, injection, authz, data leaks
tools: [Read, Glob, Grep]
model: claude-opus-4-5
---

You are a security expert. List each vulnerability with location (file:line),
type, severity (Critical/High/Medium), and a fix. You have no write tools.
```

### Explicit invocation
```
Use the security-auditor agent on src/api/ and report Critical issues only.
```

### Parallel invocation (Claude Agent SDK, Python)
```python
import asyncio
from claude_agent_sdk import query, ClaudeAgentOptions, AgentDefinition

async def main():
    async for msg in query(
        prompt="Review the repo: code-reviewer for quality, security-auditor for vulns, test-runner to verify tests.",
        options=ClaudeAgentOptions(
            allowed_tools=["Read", "Glob", "Grep", "Bash", "Agent"],
            agents={
                "code-reviewer":   AgentDefinition(description="Quality reviewer",
                                                   prompt="Analyze for quality and best practices",
                                                   tools=["Read", "Glob", "Grep"]),
                "security-auditor": AgentDefinition(description="Vuln auditor",
                                                    prompt="Find injection, authz, leak issues",
                                                    tools=["Read", "Glob", "Grep"]),
                "test-runner":     AgentDefinition(description="Run tests",
                                                   prompt="Run the suite and report failures",
                                                   tools=["Bash"]),
            },
        ),
    ):
        if hasattr(msg, "result"):
            print(msg.result)

asyncio.run(main())
```

---

## 6. Subagents vs. Normal Chat in Claude Code

| | Normal chat | Subagent |
|---|---|---|
| Context | One shared window | Isolated per agent |
| History visible to it | All of it | Only what you brief it |
| Returns | Inline messages | A single summary |
| Best for | Iterative, conversational work | Self-contained tasks |
| Risk | Context fills with noise | No memory between invocations |

**Rule of thumb:** if a task needs your conversation history → main chat. If it's self-contained research, review, or testing → subagent.

---

## 7. Subagents vs. Claude Agent SDK vs. Claude API

| | Subagents (Claude Code) | Agent SDK | Claude API |
|---|---|---|---|
| What it is | A CLI/IDE feature | A Python/TS library | The HTTP endpoint |
| You write | Markdown config | Application code | Request/response code |
| Tool loop | Handled by Claude Code | Handled by SDK | You implement it |
| Context mgmt | Automatic | Built in | Yours to build |
| Where it runs | Inside Claude Code | Your process/server | Anywhere |
| Best for | Interactive dev | Production agents, CI | Custom integrations |
| Learning curve | Easiest | Medium | Steepest |

- **Subagents** are a workflow primitive *inside* Claude Code. Markdown in, summary out.
- **Agent SDK** (`claude_agent_sdk` / `@anthropic-ai/claude-agent-sdk`) lets you build the *same kind of agent* programmatically — define agents in code, run loops, integrate MCP servers, wire it into your own app.
- **Claude API** (`anthropic` SDK or raw HTTP) is the lowest layer. You manage messages, tool definitions, and the agentic loop yourself. Maximum control, maximum work.

A useful mental model: **Claude Code = a hosted agent harness that uses the Agent SDK under the hood, which itself calls the Claude API.** Custom agents are how you extend the top layer; the SDK is how you build a *new* harness; the API is what you use when neither fits.

---

## Further Reading
- Custom subagents docs: https://code.claude.com/docs/en/sub-agents.md
- How Claude Code works: https://code.claude.com/docs/en/how-claude-code-works.md
- Agent SDK overview: https://code.claude.com/docs/en/agent-sdk/overview.md
- Messages API: https://platform.claude.com/docs/en/api/messages
