<template>
  <div class="fr-panel">

    <div class="fr-header">
      <span class="fr-title">🔍 Factor Research</span>
      <button class="fr-run-btn" :disabled="loading" @click="runFactor">
        {{ loading ? 'Running…' : 'Run Trend Strength Analysis' }}
      </button>
    </div>

    <div v-if="error" class="fr-error">{{ error }}</div>

    <!-- CHANGE 1: Research history banner -->
    <div v-if="result" class="fr-history-banner">
      📋 <strong>Research History</strong> — This tab documents the trend strength factor analysis
      that led to <strong>Finding #1</strong>. It explains why the Screener excludes
      strong-trending stocks. It is research evidence, not a trading tool.
    </div>

    <!-- Hypothesis -->
    <div v-if="result" class="fr-hypothesis">
      <div class="fr-hyp-label">HYPOTHESIS</div>
      <div class="fr-hyp-text">{{ result.hypothesis }}</div>
    </div>

    <!-- CHANGE 2: Bucket summary cards — weak-trend labeled as observed not validated -->
    <div v-if="result" class="fr-buckets">
      <div
        v-for="b in result.buckets"
        :key="b.bucket"
        class="fr-bucket-card"
        :class="bucketCardClass(b)"
      >
        <div class="fr-bc-label">{{ b.bucket }}</div>
        <!-- CHANGE 2: Validation status label per bucket -->
        <div class="fr-bc-status" :class="bucketStatusClass(b)">
          {{ bucketStatusLabel(b) }}
        </div>
        <div class="fr-bc-beat">{{ b.beatCount }}/{{ b.total }} beat B&H</div>
        <div class="fr-bc-rate" :class="b.beatRate >= 50 ? 'td-positive' : 'td-negative'">
          {{ b.beatRate }}%
        </div>
        <div class="fr-bc-adv-label">Median advantage</div>
        <div class="fr-bc-adv" :class="b.medianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
          {{ b.medianAdvantage >= 0 ? '+' : '' }}{{ b.medianAdvantage.toFixed(1) }}%
        </div>
      </div>
    </div>

    <!-- CHANGE 3: Factor Finding rewritten to reflect final validated truth -->
    <div v-if="finding" class="fr-finding">
      <div class="fr-finding-label">FACTOR FINDING — FINAL VALIDATED TRUTH</div>
      <div class="fr-finding-text">{{ finding }}</div>
    </div>

    <!-- Per-stock table -->
    <div v-if="result" class="fr-table-wrap">
      <table class="fr-table">
        <thead>
          <tr>
            <th>Stock</th>
            <th>Trend bucket</th>
            <th>B&H return</th>
            <th>RSI return</th>
            <th>Advantage</th>
            <th>Beat?</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in result.perStock" :key="s.symbol"
              :class="s.beat ? 'row-beat' : 'row-miss'">
            <td class="td-symbol">{{ s.symbol }}</td>
            <td class="td-bucket">{{ s.trendBucket }}</td>
            <td>{{ s.bahReturn.toFixed(1) }}%</td>
            <td>{{ s.rsiReturn.toFixed(1) }}%</td>
            <td :class="s.advantage >= 0 ? 'td-positive' : 'td-negative'">
              {{ s.advantage >= 0 ? '+' : '' }}{{ s.advantage.toFixed(1) }}%
            </td>
            <td>
              <span class="verdict-chip" :class="s.beat ? 'chip-beat' : 'chip-miss'">
                {{ s.beat ? '✅' : '❌' }}
              </span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- CHANGE 4: Conclusion banner + Findings link -->
    <div v-if="result" class="fr-conclusion-banner">
      <div class="fr-conclusion-title">📌 Research Conclusion</div>
      <div class="fr-conclusion-text">
        Use trend strength only to <strong>exclude strong-trending stocks (B&amp;H &gt; 300%)</strong>.
        Weak-trend outperformance was observed in this period but was not sufficiently validated
        across multiple market regimes to become an entry rule.
        Do not filter for weak-trend stocks — only exclude strong-trend stocks.
      </div>
      <div class="fr-conclusion-action">
        For the actionable rule →
        <button class="fr-findings-link" @click="goToFindings">
          View Finding #1 in Findings Library →
        </button>
      </div>
    </div>

    <div v-if="!result && !loading && !error" class="fr-empty">
      Click "Run Trend Strength Analysis" to test whether RSI works better
      on low-trend stocks than high-trend stocks.
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const loading = ref(false)
const error   = ref('')
const result  = ref(null)

const API_BASE = 'http://localhost:60363'

// CHANGE 4: Navigate to Findings tab
function goToFindings() {
  window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'findings' }))
}

async function runFactor() {
  loading.value = true
  error.value   = ''
  result.value  = null
  try {
    const res  = await fetch(`${API_BASE}/research/factor/trend-strength`)
    const data = await res.json()
    if (data.error) { error.value = data.error; return }
    result.value = data
  } catch (e) {
    error.value = `Could not run factor research: ${e.message}`
  } finally {
    loading.value = false
  }
}

// CHANGE 2: Bucket card styling — strong-trend exclusion is the validated rule
function bucketCardClass(b) {
  if (b.bucket.startsWith('Strong')) return 'card-miss'
  if (b.bucket.startsWith('Medium')) return 'card-partial'
  // Weak: observed but not validated as entry rule — use partial styling, not beat
  return 'card-observed'
}

function bucketStatusLabel(b) {
  if (b.bucket.startsWith('Strong')) return '✅ Validated — excluded by Finding #1'
  if (b.bucket.startsWith('Medium')) return '⚪ Intermediate — no rule'
  return '⚠️ Observed — not validated as entry rule'
}

function bucketStatusClass(b) {
  if (b.bucket.startsWith('Strong')) return 'status-validated'
  if (b.bucket.startsWith('Medium')) return 'status-neutral'
  return 'status-observed'
}

// CHANGE 3: Finding text rewritten to reflect final validated truth
// The original computed celebrated weak-trend success — that is misleading.
// The actual validated rule is: exclude strong-trend stocks (>300% B&H).
// Weak-trend outperformance was observed but did not survive cross-period validation.
const finding = computed(() => {
  if (!result.value?.buckets?.length) return ''
  const weak   = result.value.buckets.find(b => b.bucket.startsWith('Weak'))
  const strong = result.value.buckets.find(b => b.bucket.startsWith('Strong'))
  if (!weak || !strong) return ''

  const diff = Math.round(weak.beatRate - strong.beatRate)

  return `Strong-trend exclusion validated. RSI beat buy-and-hold in ${strong.beatRate}% ` +
         `of strong-trend stocks vs ${weak.beatRate}% of weak-trend stocks — a ${diff}% gap. ` +
         `The strong-trend exclusion (>300% 10-year B&H return) is the only rule that ` +
         `consistently survived cross-period validation and became Finding #1. ` +
         `Weak-trend outperformance (${weak.beatRate}% beat rate) was observed in this period ` +
         `but was not sufficiently validated across multiple market regimes to become a trading rule. ` +
         `The correct application: exclude strong-trend stocks. Do not filter for weak-trend stocks.`
})
</script>

<style scoped>
.fr-panel {
  --color-bg:       var(--bg-panel);
  --color-bg2:      var(--bg-panel-item);
  --color-border:   var(--bg-panel-border);
  --color-text:     var(--text-primary);
  --color-muted:    var(--text-secondary);
  --color-accent:   var(--accent);
  --color-positive: #22c55e;
  --color-negative: #ef4444;
  --color-partial:  #eab308;

  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 1rem;
  min-width: 0;
  background: var(--color-bg);
  border-radius: 10px;
  color: var(--color-text);
}

.fr-header { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.fr-title  { font-size: 14px; font-weight: 700; }
.fr-run-btn { padding: 8px 14px; border-radius: 7px; border: none; background: var(--color-accent); color: #fff; font-size: 12px; font-weight: 600; cursor: pointer; transition: opacity 0.15s; }
.fr-run-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.fr-error { padding: 8px 10px; background: rgba(239,68,68,.12); color: var(--color-negative); border-radius: 6px; font-size: 12px; }

/* CHANGE 1: Research history banner */
.fr-history-banner {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-left: 3px solid #818cf8;
  border-radius: 0 7px 7px 0;
  color: #818cf8;
}

.fr-hypothesis { background: var(--color-bg2); border-radius: 7px; padding: 10px 12px; }
.fr-hyp-label  { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 4px; }
.fr-hyp-text   { font-size: 12px; line-height: 1.6; font-style: italic; }

/* Bucket cards */
.fr-buckets { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
.fr-bucket-card {
  background: var(--color-bg2);
  border-radius: 9px;
  padding: 12px 14px;
  border-left: 3px solid var(--color-border);
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.card-beat     { border-left-color: var(--color-positive); }
.card-partial  { border-left-color: var(--color-partial); }
.card-miss     { border-left-color: var(--color-negative); }
/* CHANGE 2: Observed-not-validated style — yellow, not green */
.card-observed { border-left-color: var(--color-partial); background: rgba(234,179,8,.04); }

.fr-bc-label     { font-size: 10px; font-weight: 700; color: var(--color-muted); margin-bottom: 2px; }
.fr-bc-beat      { font-size: 11px; color: var(--color-muted); }
.fr-bc-rate      { font-size: 22px; font-weight: 700; font-variant-numeric: tabular-nums; }
.fr-bc-adv-label { font-size: 9px; color: var(--color-muted); margin-top: 4px; }
.fr-bc-adv       { font-size: 13px; font-weight: 700; font-variant-numeric: tabular-nums; }

/* CHANGE 2: Bucket status labels */
.fr-bc-status    { font-size: 9px; font-weight: 700; padding: 2px 6px; border-radius: 3px; width: fit-content; margin-bottom: 4px; }
.status-validated { background: rgba(34,197,94,.12); color: #22c55e; }
.status-observed  { background: rgba(234,179,8,.12); color: #eab308; }
.status-neutral   { background: rgba(148,163,184,.1); color: var(--color-muted); }

/* Finding */
.fr-finding { background: var(--color-bg2); border-left: 3px solid var(--color-accent); border-radius: 7px; padding: 10px 12px; }
.fr-finding-label { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 5px; }
.fr-finding-text  { font-size: 12px; line-height: 1.6; }

/* Table */
.fr-table-wrap { overflow-x: auto; }
.fr-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.fr-table th { text-align: left; padding: 6px 8px; font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: var(--color-muted); border-bottom: 1px solid var(--color-border); white-space: nowrap; }
.fr-table td { padding: 8px; border-bottom: 1px solid var(--color-border); vertical-align: middle; }
.row-beat { background: rgba(34,197,94,.04); }
.row-miss { background: transparent; }
.fr-table tbody tr:hover { background: var(--color-bg2); }
.td-symbol   { font-weight: 700; font-family: monospace; font-size: 12px; }
.td-bucket   { font-size: 11px; color: var(--color-muted); }
.td-positive { color: var(--color-positive); font-weight: 600; }
.td-negative { color: var(--color-negative); }
.verdict-chip { font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
.chip-beat    { background: rgba(34,197,94,.15); color: var(--color-positive); }
.chip-miss    { background: rgba(239,68,68,.12); color: var(--color-negative); }

/* CHANGE 4: Conclusion banner */
.fr-conclusion-banner {
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-accent);
  border-radius: 0 8px 8px 0;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.fr-conclusion-title  { font-size: 11px; font-weight: 700; letter-spacing: 0.06em; color: var(--color-muted); text-transform: uppercase; }
.fr-conclusion-text   { font-size: 12px; line-height: 1.6; color: var(--color-text); }
.fr-conclusion-action { font-size: 11px; color: var(--color-muted); }
.fr-findings-link {
  background: none; border: none; color: var(--color-accent);
  font-size: 11px; font-weight: 700; cursor: pointer; padding: 0; text-decoration: underline;
}
.fr-findings-link:hover { opacity: 0.8; }

.fr-empty { text-align: center; padding: 2.5rem 1rem; color: var(--color-muted); font-size: 13px; line-height: 1.7; }
</style>