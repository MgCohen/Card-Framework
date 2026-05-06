# doc-graph

Reference graph viewer for `Product/features/**` docs. Two zoom levels:

- **Overview** — one node per feature, cross-feature edges aggregated into weighted feature-pair links. Bidirectional pairs (e.g. `actions ↔ engine`) get a distinct style.
- **Focus** — click a feature to zoom in. The feature renders as a center hub; its models fan out (`references: root` becomes the spoke). Cross-feature edges land on neighboring (still-collapsed) feature nodes. Click a model node to open its `.md` in a modal.

## Build

```sh
cd Tools/doc-graph
npm install
npm run build      # parses Product/features/** -> graph.json
```

`graph.json` is committed so the page works straight from a clone.

## View

Open `Tools/doc-graph/index.html` in a browser. Either:

- Direct `file://` open (markdown is inlined into `graph.json`, so no fetch needed beyond `graph.json` itself — but some browsers block `fetch()` on `file://`. If so, use a server.)
- Or any static server, e.g. `npx serve .` from this dir, then visit the printed URL.

## Data source

| Source | Used for |
|---|---|
| `Product/features/<f>/<f>.md` front-matter (`status`) and body | feature node + modal content |
| `Product/features/<f>/models/*.md` front-matter (`status`, `references`, `tags`) and body | model node, edges, modal content |
| `Product/ledgers/FEATURE-MAP.md` Feature index table (rows tagged `**considered**`) | placeholder (dashed) feature nodes for unstarted work |

References resolve in this order: `root` → edge to feature node; full path → edge to model; `<feature>/<NN>` shorthand → best-effort match by feature + numeric prefix. Unresolved references print a warning during build.

Decisions (`features/<f>/decisions/*.md`) are not nodes by design.

## v2 nice-to-haves

- Toggle for body-link edges (markdown `[...](features/...)` references) as weak edges.
- Custom rendered modal view (parsed `.md` instead of raw render).
- Glossary-term layer.
- Auto-rebuild on `Product/` changes.
