#!/usr/bin/env node
/*
 * Doc graph builder.
 *
 * Walks Product/features/<feature>/(<feature>.md|models/*.md) and
 * Product/ledgers/FEATURE-MAP.md, then writes graph.json.
 *
 * Schema:
 *   {
 *     features: [{ id, label, status, path|null, markdown|null }],
 *     models:   [{ id, label, feature, status, tags, path, markdown }],
 *     edges:    [{ kind: 'model-model'|'model-root',
 *                  source, target, crossFeature }]
 *   }
 */

const fs = require('fs');
const path = require('path');

// Minimal front-matter parser. The docs use `[#tag1, #tag2]` which YAML
// rejects (# starts a comment), so we don't run a real YAML parser.
// Schema we accept:
//   key: scalar
//   key: [item, item, ...]
//   key:
//     - item
//     - item
const stripQuotes = (s) => {
  if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
    return s.slice(1, -1);
  }
  return s;
};

const parseFrontMatter = (raw) => {
  const m = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/);
  if (!m) return { data: {}, content: raw };
  const block = m[1];
  const content = m[2];
  const data = {};
  let pendingKey = null;
  let pendingList = null;
  const flush = () => {
    if (pendingKey !== null && pendingList !== null) {
      data[pendingKey] = pendingList;
    }
    pendingKey = null;
    pendingList = null;
  };
  for (const line of block.split(/\r?\n/)) {
    if (pendingList) {
      const item = line.match(/^\s+-\s*(.+?)\s*$/);
      if (item) {
        pendingList.push(stripQuotes(item[1]));
        continue;
      }
      flush();
    }
    const kv = line.match(/^([A-Za-z_][\w-]*)\s*:\s*(.*)$/);
    if (!kv) continue;
    const [, key, vRaw] = kv;
    const v = vRaw.trim();
    if (v === '') {
      pendingKey = key;
      pendingList = [];
      continue;
    }
    const inline = v.match(/^\[(.*)\]\s*$/);
    if (inline) {
      const items = inline[1].split(',').map((s) => stripQuotes(s.trim())).filter(Boolean);
      data[key] = items;
      continue;
    }
    data[key] = stripQuotes(v);
  }
  flush();
  return { data, content };
};

const repoRoot = path.resolve(__dirname, '..', '..');
const featuresDir = path.join(repoRoot, 'Product', 'features');
const featureMapPath = path.join(repoRoot, 'Product', 'ledgers', 'FEATURE-MAP.md');
const outPath = path.join(__dirname, 'graph.json');

const warnings = [];
const warn = (msg) => warnings.push(msg);

const firstHeading = (md) => {
  const m = md.match(/^#\s+(.+?)\s*$/m);
  return m ? m[1].trim() : null;
};

const normalizeReferences = (raw) => {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map(String).map((s) => s.trim()).filter(Boolean);
  return [String(raw).trim()].filter(Boolean);
};

const modelIdFromPath = (relPath) => {
  // features/<feature>/models/<basename>.md  ->  <feature>/<basename>
  const m = relPath.match(/^features\/([^/]+)\/models\/(.+)\.md$/);
  if (!m) return null;
  return `${m[1]}/${m[2]}`;
};

const featureIdFromPath = (relPath) => {
  const m = relPath.match(/^features\/([^/]+)\//);
  return m ? m[1] : null;
};

// Parse considered features out of FEATURE-MAP.md's Feature index table.
const parseConsideredFeatures = () => {
  if (!fs.existsSync(featureMapPath)) return [];
  const text = fs.readFileSync(featureMapPath, 'utf8');
  const considered = [];
  for (const line of text.split('\n')) {
    if (!line.startsWith('|')) continue;
    const cells = line.split('|').map((c) => c.trim());
    // ['', feature, status, models, purpose, '']
    if (cells.length < 5) continue;
    const featureCell = cells[1];
    const statusCell = cells[2];
    if (!/considered/i.test(statusCell)) continue;
    // feature cell is either `name` or **considered** etc.; pull the bare name.
    const nameMatch = featureCell.match(/`([^`]+)`/);
    const name = nameMatch ? nameMatch[1] : featureCell.replace(/[*`]/g, '').trim();
    if (!name || name.toLowerCase() === 'feature') continue;
    considered.push({ id: name, label: name, status: 'considered', path: null, markdown: null });
  }
  return considered;
};

const main = () => {
  if (!fs.existsSync(featuresDir)) {
    console.error(`features dir not found: ${featuresDir}`);
    process.exit(1);
  }

  const featureDirs = fs
    .readdirSync(featuresDir, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
    .sort();

  /** @type {Map<string, any>} */
  const features = new Map();
  /** @type {Map<string, any>} */
  const models = new Map();
  /** @type {Map<string, string>} */
  const modelPathToId = new Map(); // 'features/effects/models/01-async-dispatch.md' -> 'effects/01-async-dispatch'
  const edges = [];

  for (const feature of featureDirs) {
    const featureRel = `features/${feature}`;
    const featureRootRel = `${featureRel}/${feature}.md`;
    const featureRootAbs = path.join(featuresDir, feature, `${feature}.md`);

    let label = feature;
    let status = 'settled';
    let markdown = null;
    let rootPath = null;
    if (fs.existsSync(featureRootAbs)) {
      const raw = fs.readFileSync(featureRootAbs, 'utf8');
      const fm = parseFrontMatter(raw);
      if (fm.data && typeof fm.data.status === 'string') status = fm.data.status;
      const heading = firstHeading(fm.content);
      if (heading) label = heading.replace(/\s+—\s+feature\s*$/i, '').trim() || feature;
      markdown = raw;
      rootPath = featureRootRel;
    } else {
      warn(`feature '${feature}' has no ${feature}.md; using directory name as label`);
    }

    features.set(feature, {
      id: feature,
      label,
      status,
      path: rootPath,
      markdown,
    });

    const modelsDir = path.join(featuresDir, feature, 'models');
    if (!fs.existsSync(modelsDir)) continue;
    const modelFiles = fs
      .readdirSync(modelsDir)
      .filter((f) => f.endsWith('.md'))
      .sort();
    for (const file of modelFiles) {
      const modelRel = `features/${feature}/models/${file}`;
      const modelAbs = path.join(modelsDir, file);
      const raw = fs.readFileSync(modelAbs, 'utf8');
      const fm = parseFrontMatter(raw);
      const id = modelIdFromPath(modelRel);
      if (!id) {
        warn(`could not derive id for ${modelRel}`);
        continue;
      }
      const heading = firstHeading(fm.content) || file.replace(/\.md$/, '');
      const data = fm.data || {};
      const node = {
        id,
        label: heading,
        feature,
        status: typeof data.status === 'string' ? data.status : 'settled',
        tags: Array.isArray(data.tags) ? data.tags.map(String) : [],
        path: modelRel,
        markdown: raw,
        _refs: normalizeReferences(data.references),
      };
      models.set(id, node);
      modelPathToId.set(modelRel, id);
    }
  }

  // Considered features (no folder).
  for (const cf of parseConsideredFeatures()) {
    if (features.has(cf.id)) continue;
    features.set(cf.id, cf);
  }

  // Resolve edges.
  for (const m of models.values()) {
    for (const ref of m._refs) {
      if (ref === 'root') {
        edges.push({
          kind: 'model-root',
          source: m.id,
          target: m.feature,
          crossFeature: false,
        });
        continue;
      }
      // Full path form.
      const targetId = modelPathToId.get(ref);
      if (targetId) {
        const target = models.get(targetId);
        edges.push({
          kind: 'model-model',
          source: m.id,
          target: targetId,
          crossFeature: target.feature !== m.feature,
        });
        continue;
      }
      // Shorthand <feature>/<NN> form (best-effort).
      const sm = ref.match(/^([a-z][a-z0-9-]*)\/(\d+)$/i);
      if (sm) {
        const [, sf, snum] = sm;
        const candidate = [...models.values()].find(
          (n) => n.feature === sf && /^(\d+)/.exec(n.id.split('/')[1])?.[1] === snum.replace(/^0+/, '').padStart(2, '0')
        ) || [...models.values()].find(
          (n) => n.feature === sf && n.id.split('/')[1].startsWith(snum.padStart(2, '0'))
        );
        if (candidate) {
          edges.push({
            kind: 'model-model',
            source: m.id,
            target: candidate.id,
            crossFeature: candidate.feature !== m.feature,
          });
          continue;
        }
      }
      warn(`unresolved reference '${ref}' from ${m.path}`);
    }
    delete m._refs;
  }

  const output = {
    generatedAt: new Date().toISOString(),
    features: [...features.values()].sort((a, b) => a.id.localeCompare(b.id)),
    models: [...models.values()].sort((a, b) => a.id.localeCompare(b.id)),
    edges,
    warnings,
  };

  fs.writeFileSync(outPath, JSON.stringify(output, null, 2) + '\n', 'utf8');
  console.log(
    `wrote ${path.relative(repoRoot, outPath)}: ${output.features.length} features, ` +
    `${output.models.length} models, ${output.edges.length} edges` +
    (warnings.length ? ` (${warnings.length} warnings)` : '')
  );
  for (const w of warnings) console.warn(`  warn: ${w}`);
};

main();
