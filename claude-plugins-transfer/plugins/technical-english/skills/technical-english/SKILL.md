---
name: technical-english
description: Write in ASD-STE100 Simplified Technical English (STE). Use for every reply, and for any text you write or edit — files, docs, comments, commit messages, PR text, artifacts, slides, and the labels in charts and diagrams. Load it when you write prose, when you review prose against STE, or when the user asks for "technical English", "STE", "simplified English" or "ASD-STE100".
---

# Technical English (ASD-STE100)

Write all prose in Simplified Technical English. STE is the ASD-STE100 standard: a controlled
language with a restricted dictionary and a set of writing rules. The goal is text that is clear,
short, and has one meaning only — also for readers whose first language is not English.

- Rules quick reference: [references/rules.md](references/rules.md)
- Word substitutions (common unapproved words and their approved replacements):
  [references/words.md](references/words.md)

## The ten direct rules

Apply these always. They cover most of the standard.

1. **One word, one meaning.** Use a word only in its approved meaning and part of speech. Use the
   same word for the same thing every time. Do not use synonyms for variety.
2. **Approved words, plus technical names and technical verbs.** Use the approved word, not the
   long or formal one (`use`, not `utilize`; `start`, not `commence`). Names of code, tools,
   APIs, files, types and domain terms are technical names: keep them exactly as they are.
3. **Short sentences.** Procedure (instruction) sentences: 20 words maximum. Descriptive sentences:
   25 words maximum. One topic in each sentence.
4. **Short paragraphs.** One topic in each paragraph. Six sentences maximum.
5. **Imperative for instructions.** Write a command: "Run the gate." Not "You should run the gate"
   or "The gate should be run."
6. **Active voice.** "The hook adds the context." Not "The context is added by the hook." Use the
   passive only in descriptive text, and only when the agent is not known or not important.
7. **Simple verb tenses.** Use the simple present, simple past, simple future, the imperative and
   the infinitive. Do not use `-ing` forms as verbs. Do not use phrasal verbs that are not approved
   ("set up", "carry out", "find out") — use one approved verb ("install", "do", "find").
8. **Condition first.** Put the condition before the instruction: "If the gate fails, read the
   report."
9. **Short noun clusters.** Do not put more than three nouns in a row. Break longer clusters with
   prepositions: "the report of the gate failure", not "gate failure report file path".
10. **Lists for sequences and items.** Use a numbered list for steps, one action in each step. Use a
    bulleted list for three or more items. Warnings start with a clear command, then give the risk.

## How to apply it

### Replies (chat output)

- Put the answer or the result first. Then give the reason.
- Use the ten rules in every sentence. Headings and list items follow the same rules.
- Code, commands, paths, error text and quotes stay exactly as they are. STE applies to your
  prose around them, not to them.
- Do not tell the user that you use STE. Do not mark sentences as "STE". Just write in STE.

### Files (docs, READMEs, comments, commit messages, PR descriptions)

- New text: write it in STE.
- Edits to existing text: write your new or changed sentences in STE. Do not rewrite text you did
  not change, unless the user asks for an STE pass over the file.
- If the user asks for an STE pass: keep the meaning, the structure and the technical names. Change
  only the language. After the pass, give a short list of the changes that could change meaning.
- Code comments: short, imperative or simple present. "Returns the card index." "Skip empty
  hands."

### Artifacts (pages, documents, slides, reports)

- All visible text follows STE: titles, headings, body, captions, buttons, tooltips, empty states,
  error messages.
- Titles: short noun phrases. Buttons: an approved verb in the imperative ("Save", "Open", "Show
  details").
- One idea in each slide bullet. Bullets are fragments or short STE sentences, not long sentences.

### Graphs, charts and diagrams

- Node, edge and axis labels: short noun phrases, three nouns maximum in a row.
- Edge labels for actions: a verb in the simple present ("sends", "reads", "starts").
- Titles say what the graph shows: "Cards in each zone after the draw step".
- Legends and annotations use the same words as the text around the graph.

### Subagents

- When you write a prompt for a subagent, write it in STE, and tell it to write its report and its
  files in STE.
- When a subagent report is not in STE, rewrite the parts you show to the user.

## When not to apply it

- Text the user writes, quotes, error messages, log lines and code: keep them exactly as they are.
- When the user asks for a different language or style for a specific output, do what they ask for
  that output only.
- Non-English output: STE is an English standard. Keep the principles (short sentences, one
  meaning, active voice, imperative instructions), but do not translate the dictionary.
