# Technical English (ASD-STE100)

This plugin makes Claude write in ASD-STE100 Simplified Technical English (STE).

## Contents

| Part | File | What it does |
|---|---|---|
| Skill | `skills/technical-english/` | The ten direct rules, how to apply STE to replies, files, artifacts, graphs and subagents, a rules quick reference, and a word substitution list. |
| Output style | `output-styles/technical-english.md` | Forced for the plugin (`force-for-plugin: true`). It keeps the default coding instructions and adds the STE rules to the system prompt. |
| Hook: SessionStart | `hooks/hooks.json` | Adds the core rules at startup, resume, clear and compact. |
| Hook: UserPromptSubmit | `hooks/hooks.json` | Adds a one-line reminder on each turn. |
| Hook: SubagentStart | `hooks/hooks.json` | Adds the core rules to each subagent, and tells it to write its report and files in STE. |

The hooks use only `cat` on prepared JSON files. They work on macOS, Linux and Windows (Git Bash).

## Install

Synced to your claude.ai account (follows you to each computer):

1. Upload the plugin on claude.ai as a personal plugin (or add the `MgCohen/claude-plugins`
   marketplace there), then turn it on.
2. Start Claude Code while you are signed in with your claude.ai account (v2.1.273 or later).
   The plugin loads as `technical-english@synced`.

From the GitHub marketplace (user scope, one time on each computer):

```
/plugin install technical-english --marketplace MgCohen/claude-plugins
```

To turn off the synced copy on one computer only: `claude plugin disable technical-english@synced`.

## Local test

```
claude --plugin-dir ./plugins/technical-english
```

## Source of the rules

The rules are a summary of ASD-STE100. They are not the standard. Get the current issue and the
full dictionary from asd-ste100.org.
