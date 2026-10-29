# claude-plugins

The personal Claude Code plugin marketplace of Matheus Cohen. All plugins here are for the user
scope: they are not specific to a repository or to a computer.

## Plugins

| Plugin | What it does |
|---|---|
| [technical-english](plugins/technical-english/) | Writes all output, files, artifacts and diagram text in ASD-STE100 Simplified Technical English. |

## Use the marketplace

There are two ways to get these plugins. Use the first way if you can.

### 1. Synced to your claude.ai account

Plugins that you turn on for your claude.ai account load in each Claude Code session where you
sign in with that account. They load as `<plugin>@synced`.

1. On claude.ai, upload the plugin as a personal plugin, or add this repository as a marketplace
   if claude.ai shows that option. Then turn the plugin on.
2. Start Claude Code (v2.1.273 or later) while you are signed in with claude.ai.

Sync does not run when you sign in with an API key, `ANTHROPIC_AUTH_TOKEN` or
`CLAUDE_CODE_OAUTH_TOKEN`, or when you start with `--bare` or
`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`.

To make an upload file for one plugin:

```
cd plugins/technical-english && zip -r ../../technical-english.zip . -x '.*.swp'
```

### 2. From GitHub, on each computer

Run this one time on each computer. Claude Code writes it to your user settings
(`~/.claude/settings.json`).

```
claude plugin marketplace add MgCohen/claude-plugins
claude plugin install technical-english@mgcohen-plugins
```

To get new versions: `claude plugin marketplace update mgcohen-plugins`, then restart Claude Code.

Do not turn on the same plugin both ways on one computer. If you do, disable one copy, for example
`claude plugin disable technical-english@mgcohen-plugins`.

## Add a plugin

1. Make a folder `plugins/<name>/` with `.claude-plugin/plugin.json` (`name`, `version`,
   `description`, `author`, `license`) and the components at the plugin root: `skills/`,
   `hooks/hooks.json`, `agents/`, `output-styles/`.
2. Add an entry to `.claude-plugin/marketplace.json` with `name`, `source`
   (`./plugins/<name>`), `description` and `category`. Keep the version in `plugin.json` only.
3. Validate:

   ```
   claude plugin validate .
   claude plugin validate plugins/<name>
   ```

4. Test it locally: `claude --plugin-dir plugins/<name>`.
5. Add a row to the table above. Commit and push.

## Change a plugin

1. Make the change.
2. Increase `version` in `plugins/<name>/.claude-plugin/plugin.json`. If you do not change the
   version, installed copies do not update.
3. Validate, commit and push. For the synced copy, upload the new version on claude.ai.
