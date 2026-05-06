/* global cytoscape, marked */

const STATUS_COLOR = {
  settled: '#6aa9ff',
  draft: '#ffd166',
  implemented: '#4ade80',
  considered: '#8a93a6',
  superseded: '#b07cff',
  abandoned: '#ef4444',
};

const DEFAULT_STATUS_COLOR = STATUS_COLOR.settled;

const state = {
  data: null,
  mode: { kind: 'overview' },
  cy: null,
};

async function loadData() {
  const res = await fetch('graph.json', { cache: 'no-cache' });
  if (!res.ok) throw new Error(`failed to load graph.json: ${res.status}`);
  return res.json();
}

function statusColor(status) {
  return STATUS_COLOR[status] || DEFAULT_STATUS_COLOR;
}

function buildOverviewElements(data) {
  const nodes = data.features.map((f) => ({
    data: {
      id: `f:${f.id}`,
      kind: 'feature',
      featureId: f.id,
      label: f.label,
      status: f.status,
      hasContent: !!f.markdown,
    },
    classes: `feature feature-${f.status}`,
  }));

  // Aggregate cross-feature model→model edges into feature pairs.
  const pairs = new Map(); // key 'a||b' (a,b sorted)  -> { a, b, abCount, baCount }
  for (const e of data.edges) {
    if (e.kind !== 'model-model' || !e.crossFeature) continue;
    const sf = e.source.split('/')[0];
    const tf = e.target.split('/')[0];
    if (sf === tf) continue;
    const [lo, hi] = [sf, tf].sort();
    const key = `${lo}||${hi}`;
    const rec = pairs.get(key) || { a: lo, b: hi, abCount: 0, baCount: 0 };
    if (sf === lo) rec.abCount += 1;
    else rec.baCount += 1;
    pairs.set(key, rec);
  }

  const edges = [];
  for (const { a, b, abCount, baCount } of pairs.values()) {
    const isCycle = abCount > 0 && baCount > 0;
    const total = abCount + baCount;
    if (isCycle) {
      edges.push({
        data: {
          id: `e:${a}~${b}`,
          source: `f:${a}`,
          target: `f:${b}`,
          weight: total,
          isCycle: true,
        },
        classes: 'overview-edge cycle',
      });
    } else {
      const src = abCount > 0 ? a : b;
      const dst = abCount > 0 ? b : a;
      edges.push({
        data: {
          id: `e:${src}->${dst}`,
          source: `f:${src}`,
          target: `f:${dst}`,
          weight: total,
        },
        classes: 'overview-edge',
      });
    }
  }

  return [...nodes, ...edges];
}

function buildFocusedElements(data, focusId) {
  const focusedFeature = data.features.find((f) => f.id === focusId);
  if (!focusedFeature) return buildOverviewElements(data);

  const focusedModels = data.models.filter((m) => m.feature === focusId);
  const focusedModelIds = new Set(focusedModels.map((m) => m.id));

  // Cross-feature edges from/to focused models -> partner features that need to render.
  const partnerFeatures = new Set();
  const crossEdges = []; // model -> partner feature
  for (const e of data.edges) {
    if (e.kind !== 'model-model') continue;
    const sf = e.source.split('/')[0];
    const tf = e.target.split('/')[0];
    if (sf === focusId && tf !== focusId) {
      partnerFeatures.add(tf);
      crossEdges.push({ source: e.source, target: tf, dir: 'out' });
    } else if (tf === focusId && sf !== focusId) {
      partnerFeatures.add(sf);
      crossEdges.push({ source: sf, target: e.target, dir: 'in' });
    }
  }

  const nodes = [];

  // Focus feature node (center hub).
  nodes.push({
    data: {
      id: `f:${focusedFeature.id}`,
      kind: 'feature',
      featureId: focusedFeature.id,
      label: focusedFeature.label,
      status: focusedFeature.status,
      hasContent: !!focusedFeature.markdown,
      isFocus: true,
    },
    classes: `feature feature-${focusedFeature.status} focus`,
  });

  // Focused feature's model nodes.
  for (const m of focusedModels) {
    nodes.push({
      data: {
        id: `m:${m.id}`,
        kind: 'model',
        modelId: m.id,
        label: m.label,
        status: m.status,
        feature: m.feature,
      },
      classes: `model model-${m.status}`,
    });
  }

  // Partner feature nodes (collapsed).
  for (const pf of partnerFeatures) {
    const f = data.features.find((x) => x.id === pf);
    if (!f) continue;
    nodes.push({
      data: {
        id: `f:${f.id}`,
        kind: 'feature',
        featureId: f.id,
        label: f.label,
        status: f.status,
        hasContent: !!f.markdown,
      },
      classes: `feature feature-${f.status}`,
    });
  }

  const edges = [];

  // Intra-feature edges.
  for (const e of data.edges) {
    if (e.kind === 'model-root' && e.target === focusId) {
      edges.push({
        data: {
          id: `er:${e.source}->${e.target}`,
          source: `m:${e.source}`,
          target: `f:${e.target}`,
        },
        classes: 'root-edge',
      });
      continue;
    }
    if (e.kind === 'model-model' && !e.crossFeature) {
      if (focusedModelIds.has(e.source) && focusedModelIds.has(e.target)) {
        edges.push({
          data: {
            id: `em:${e.source}->${e.target}`,
            source: `m:${e.source}`,
            target: `m:${e.target}`,
          },
          classes: 'intra-edge',
        });
      }
    }
  }

  // Cross-feature edges (model → partner feature node).
  for (const ce of crossEdges) {
    if (ce.dir === 'out') {
      edges.push({
        data: {
          id: `ex:${ce.source}->f:${ce.target}`,
          source: `m:${ce.source}`,
          target: `f:${ce.target}`,
        },
        classes: 'cross-edge',
      });
    } else {
      edges.push({
        data: {
          id: `ex:f:${ce.source}->${ce.target}`,
          source: `f:${ce.source}`,
          target: `m:${ce.target}`,
        },
        classes: 'cross-edge',
      });
    }
  }

  return [...nodes, ...edges];
}

function cyStyle() {
  return [
    {
      selector: 'node.feature',
      style: {
        shape: 'round-rectangle',
        'background-color': (n) => statusColor(n.data('status')),
        'background-opacity': 0.92,
        'border-width': 2,
        'border-color': '#0b0d12',
        label: 'data(label)',
        color: '#0b0d12',
        'font-weight': 600,
        'font-size': 13,
        'text-valign': 'center',
        'text-halign': 'center',
        'text-wrap': 'wrap',
        'text-max-width': 120,
        padding: 10,
        width: 'label',
        height: 36,
      },
    },
    {
      selector: 'node.feature-considered',
      style: {
        'background-color': '#161922',
        'background-opacity': 1,
        'border-style': 'dashed',
        'border-color': STATUS_COLOR.considered,
        color: STATUS_COLOR.considered,
      },
    },
    {
      selector: 'node.feature.focus',
      style: {
        'border-color': '#ffffff',
        'border-width': 3,
        'font-size': 15,
        height: 44,
      },
    },
    {
      selector: 'node.model',
      style: {
        shape: 'round-rectangle',
        'background-color': (n) => statusColor(n.data('status')),
        'background-opacity': 0.18,
        'border-width': 1.5,
        'border-color': (n) => statusColor(n.data('status')),
        label: 'data(label)',
        color: '#e6e8ee',
        'font-size': 11,
        'text-valign': 'center',
        'text-halign': 'center',
        'text-wrap': 'wrap',
        'text-max-width': 160,
        padding: 8,
        width: 'label',
        height: 30,
      },
    },
    {
      selector: 'edge',
      style: {
        width: 1.5,
        'line-color': '#3b455c',
        'target-arrow-color': '#3b455c',
        'target-arrow-shape': 'triangle',
        'curve-style': 'bezier',
        opacity: 0.85,
      },
    },
    {
      selector: 'edge.overview-edge',
      style: {
        width: (e) => Math.min(6, 1 + (e.data('weight') || 1)),
        'line-color': '#5b6a8c',
        'target-arrow-color': '#5b6a8c',
      },
    },
    {
      selector: 'edge.overview-edge.cycle',
      style: {
        'line-color': STATUS_COLOR.settled,
        'target-arrow-color': STATUS_COLOR.settled,
        'source-arrow-color': STATUS_COLOR.settled,
        'source-arrow-shape': 'triangle',
        'line-style': 'solid',
        width: 4,
      },
    },
    {
      selector: 'edge.root-edge',
      style: {
        'line-color': '#3a4564',
        'target-arrow-color': '#3a4564',
        'line-style': 'dashed',
        opacity: 0.55,
      },
    },
    {
      selector: 'edge.cross-edge',
      style: {
        'line-color': STATUS_COLOR.draft,
        'target-arrow-color': STATUS_COLOR.draft,
        width: 2,
      },
    },
    {
      selector: ':selected',
      style: {
        'overlay-color': '#ffffff',
        'overlay-opacity': 0.08,
        'overlay-padding': 6,
      },
    },
  ];
}

function layoutFor(modeKind) {
  if (modeKind === 'focused') {
    return {
      name: 'concentric',
      concentric: (n) => (n.data('isFocus') ? 10 : n.data('kind') === 'model' ? 5 : 1),
      levelWidth: () => 1,
      minNodeSpacing: 30,
      animate: true,
      animationDuration: 350,
    };
  }
  return {
    name: 'fcose',
    quality: 'default',
    animate: true,
    animationDuration: 350,
    nodeRepulsion: 6500,
    idealEdgeLength: 140,
    nodeSeparation: 120,
    randomize: false,
    fit: true,
    padding: 30,
  };
}

function setMode(newMode) {
  state.mode = newMode;
  render();
  document.getElementById('mode-label').textContent =
    newMode.kind === 'overview' ? 'overview' : `focus: ${newMode.id}`;
}

function render() {
  const isFocus = state.mode.kind === 'focus';
  const els = isFocus
    ? buildFocusedElements(state.data, state.mode.id)
    : buildOverviewElements(state.data);

  state.cy.elements().remove();
  state.cy.add(els);
  state.cy.layout(layoutFor(isFocus ? 'focused' : 'overview')).run();
}

function openModal({ title, sub, markdown }) {
  document.getElementById('modal-title').textContent = title;
  document.getElementById('modal-sub').textContent = sub || '';
  const body = document.getElementById('modal-body');
  body.innerHTML = markdown ? marked.parse(markdown) : '<p><em>No content.</em></p>';
  body.scrollTop = 0;
  const modal = document.getElementById('modal');
  modal.classList.remove('hidden');
  modal.setAttribute('aria-hidden', 'false');
}

function closeModal() {
  const modal = document.getElementById('modal');
  modal.classList.add('hidden');
  modal.setAttribute('aria-hidden', 'true');
}

function bindEvents() {
  document.getElementById('btn-overview').addEventListener('click', () =>
    setMode({ kind: 'overview' })
  );
  document.getElementById('btn-fit').addEventListener('click', () => state.cy.fit(undefined, 30));
  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal').addEventListener('click', (ev) => {
    if (ev.target.id === 'modal') closeModal();
  });
  document.addEventListener('keydown', (ev) => {
    if (ev.key === 'Escape') closeModal();
  });

  state.cy.on('tap', 'node', (ev) => {
    const n = ev.target;
    const kind = n.data('kind');
    if (kind === 'feature') {
      const fid = n.data('featureId');
      // In overview, click expands. In focused mode, the focus node opens its modal;
      // a different feature node re-focuses to that feature.
      if (state.mode.kind === 'overview') {
        const f = state.data.features.find((x) => x.id === fid);
        if (f && (state.data.models.some((m) => m.feature === fid) || f.markdown)) {
          setMode({ kind: 'focus', id: fid });
        }
        return;
      }
      if (state.mode.kind === 'focus' && state.mode.id === fid) {
        const f = state.data.features.find((x) => x.id === fid);
        if (f && f.markdown) {
          openModal({ title: f.label, sub: f.path, markdown: f.markdown });
        }
        return;
      }
      // Different feature in focus mode -> re-focus.
      setMode({ kind: 'focus', id: fid });
      return;
    }
    if (kind === 'model') {
      const mid = n.data('modelId');
      const m = state.data.models.find((x) => x.id === mid);
      if (m) openModal({ title: m.label, sub: m.path, markdown: m.markdown });
    }
  });

  state.cy.on('tap', (ev) => {
    if (ev.target === state.cy && state.mode.kind === 'focus') {
      // Tap on background returns to overview.
      setMode({ kind: 'overview' });
    }
  });
}

async function main() {
  state.data = await loadData();
  if (window.cytoscapeFcose) cytoscape.use(window.cytoscapeFcose);
  state.cy = cytoscape({
    container: document.getElementById('cy'),
    style: cyStyle(),
    wheelSensitivity: 0.25,
  });
  bindEvents();
  setMode({ kind: 'overview' });
}

main().catch((err) => {
  console.error(err);
  document.body.insertAdjacentHTML(
    'beforeend',
    `<pre style="color:#ef4444;padding:16px">${String(err && err.stack || err)}</pre>`
  );
});
