# CLAUDE.md

Personal Claude Code plugin marketplace. See [README.md](README.md) for the layout and the steps to
add or change a plugin.

## Rules

- Each plugin is in `plugins/<name>/` and has an entry in `.claude-plugin/marketplace.json`.
- The version is only in the `plugin.json` of the plugin. Increase it for each change.
- Before you commit, run `claude plugin validate .` and `claude plugin validate plugins/<name>`.
  Both must pass.
- Hooks must work on Windows (Git Bash), macOS and Linux. Do not use Python or Node in hooks.
- Write docs in ASD-STE100 Simplified Technical English.
