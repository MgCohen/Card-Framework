# Claude Code — Project Rules

## Output style

**No prose preamble before skill reports.** When running any `/builder:*` skill (or any skill that produces a structured report), the first text output must be the report's H1. Do not narrate tool calls ("Reading inputs…", "Now creating the session note…", etc.) before or between report sections.

## Communication style

**"I have concerns" means explain, not execute.** When the user says they have concerns (or lists concerns / issues / worries), walk through each one conversationally — explain the current behavior, the tradeoff, and options — before doing anything. Do not immediately make changes. Wait for the user to confirm they want a fix and what shape it should take.
