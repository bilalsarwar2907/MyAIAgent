<template>
  <div class="vr-panel">

    <div class="vr-header">
      <span class="vr-title">📉 Volatility Factor Research</span>
      <div class="vr-header-actions">
        <button class="vr-run-btn" :disabled="loading" @click="runVolatility">
          {{ loading ? `Running ${progress}…` : 'Run Volatility Analysis (2016–2026)' }}
        </button>
        <button
          class="vr-run-btn vr-run-btn--secondary"
          :disabled="loadingValidation || !result"
          @click="runValidation"
        >
          {{ loadingValidation ? 'Running 2006–2016…' : 'Validate 2006–2016' }}
        </button>
      </div>
    </div>

    <div v-if="error" class="vr-error">{{ error }}</div>

    <!-- Hypothesis -->
    <div v-if="result" class="vr-hypothesis">
      <div class="vr-hyp-label">HYPOTHESIS</div>
      <div class="vr-hyp-text">{{ result.hypothesis }}</div>
    </div>

    <!-- Bucket summary cards -->
    <div v-if="result" class="vr-buckets">
      <div
        v-for="b in result.buckets"
        :key="b.bucket"
        class="vr-bucket-card"
        :class="b.beatRate >= 50 ? 'card-beat' : b.beatRate >= 35 ? 'card-partial' : 'card-miss'"
      >
        <div class="vr-bc-label">{{ b.bucket }}</div>
        <div class="vr-bc-beat">{{ b.beatCount }}/{{ b.total }} beat B&H</div>
        <div class="vr-bc-rate" :class="b.beatRate >= 50 ? 'td-positive' : 'td-negative'">
          {{ b.beatRate }}%
        </div>
        <div class="vr-bc-adv-label">Median advantage</div>
        <div class="vr-bc-adv" :class="b.medianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
          {{ b.medianAdvantage >= 0 ? '+' : '' }}{{ b.medianAdvantage.toFixed(1) }}%
        </div>
        <div class="vr-bc-vol-label">Avg annualised vol</div>
        <div class="vr-bc-vol">{{ b.avgVolatility.toFixed(1) }}%</div>
      </div>
    </div>

    <!-- Finding -->
    <div v-if="result?.finding" class="vr-finding">
      <div class="vr-finding-label">VOLATILITY FINDING</div>
      <div class="vr-finding-text">{{ result.finding }}</div>
    </div>

    <!-- Validation result -->
    <div v-if="validationResult" class="vr-validation-wrap">
      <div class="vr-val-header">
        <span class="vr-val-title">🔁 Cross-Period Validation (2006–2016)</span>
        <span class="vr-val-sub">Does the pattern hold in an independent decade?</span>
      </div>

      <div class="vr-val-buckets">
        <div
          v-for="b in validationResult.buckets"
          :key="b.bucket"
          class="vr-val-card"
        >
          <div class="vr-val-bucket-label">{{ b.bucket }}</div>
          <div class="vr-val-row">
            <div class="vr-val-period">
              <div class="vr-val-period-label">2016–2026</div>
              <div class="vr-val-beat" :class="primaryBucketRate(b.bucket) >= 50 ? 'td-positive' : 'td-negative'">
                {{ primaryBucketRate(b.bucket) }}%
              </div>
            </div>
            <div class="vr-val-arrow">→</div>
            <div class="vr-val-period">
              <div class="vr-val-period-label">2006–2016</div>
              <div class="vr-val-beat" :class="b.beatRate >= 50 ? 'td-positive' : 'td-negative'">
                {{ b.beatRate }}%
              </div>
            </div>
            <div class="vr-val-verdict">
              <span
                class="verdict-chip"
                :class="b.beatRate >= 50 ? 'chip-beat' : b.beatRate >= 35 ? 'chip-partial' : 'chip-miss'"
              >
                {{ b.beatRate >= 50 ? '✅ Held' : b.beatRate >= 35 ? '⚠️ Partial' : '❌ Failed' }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <div class="vr-val-finding">
        <div class="vr-finding-label">VALIDATION FINDING</div>
        <div class="vr-finding-text">{{ validationResult.finding }}</div>
      </div>
    </div>

    <!-- Per-stock table -->
    <div v-if="result" class="vr-table-wrap">
      <table class="vr-table">
        <thead>
          <tr>
            <th>Stock</th>
            <th>Vol bucket</th>
            <th>Ann. vol</th>
            <th>B&H return</th>
            <th>RSI return</th>
            <th>Advantage</th>
            <th>Trades</th>
            <th>Beat?</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in result.perStock" :key="s.symbol"
              :class="s.beat ? 'row-beat' : 'row-miss'">
            <td class="td-symbol">{{ s.symbol }}</td>
            <td class="td-bucket">{{ s.volatilityBucket }}</td>
            <td class="td-vol">{{ s.annualisedVolatility.toFixed(1) }}%</td>
            <td>{{ s.bahReturn.toFixed(1) }}%</td>
            <td>{{ s.rsiReturn.toFixed(1) }}%</td>
            <td :class="s.advantage >= 0 ? 'td-positive' : 'td-negative'">
              {{ s.advantage >= 0 ? '+' : '' }}{{ s.advantage.toFixed(1) }}%
            </td>
            <td class="td-muted">{{ s.trades }}</td>
            <td>
              <span class="verdict-chip" :class="s.beat ? 'chip-beat' : 'chip-miss'">
                {{ s.beat ? '✅' : '❌' }}
              </span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="!result && !loading && !error" class="vr-empty">
      Click "Run Volatility Analysis" to test whether RSI works better
      on low-volatility stocks than high-volatility stocks.<br><br>
      <span class="vr-empty-detail">
        Buckets: Low (&lt;25% annualised vol) · Medium (25–50%) · High (&gt;50%)
      </span>
    </div>

  </div>
</template>

<script setup>
import { ref } from 'vue'

const loading           = ref(false)
const loadingValidation = ref(false)
const error             = ref('')
const result            = ref(null)
const validationResult  = ref(null)
const progress          = ref('')

const API_BASE = 'http://localhost:60363'

// Use the same 59-stock universe as sector/factor research
// If you don't have a composable for this, replace with the inline array
// from StockUniverse.cs
const ALL_SYMBOLS = [
  'AAPL','MSFT','GOOGL','AMZN','META','NVDA','TSLA','AMD','INTC','CRM',
  'JPM','BAC','WFC','GS','MS','BRK-B','V','MA','AXP','BLK',
  'XOM','CVX','COP','SLB','OXY','BP','TOT','EOG','PSX','VLO',
  'DAL','UAL','AAL','LUV','JBLU','BA','RTX','LMT','NOC','GD',
  'PFE','JNJ','MRK','ABBV','LLY','BMY','AMGN','GILD','REGN','BIIB',
  'AMT','PLD','CCI','EQIX','O','NEE','SO','DUK','AEP','EXC',
]

async function runVolatility() {
  loading.value  = true
  error.value    = ''
  result.value   = null
  validationResult.value = null

  const symbols = ALL_SYMBOLS
  let done = 0

  // Show progress via polling — the backend streams nothing, so we just animate
  const progressInterval = setInterval(() => {
    done = Math.min(done + 1, symbols.length)
    progress.value = `${done}/${symbols.length}`
  }, 600)

  try {
    const res  = await fetch(`${API_BASE}/api/volatility/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ symbols })
    })
    const data = await res.json()
    if (data.error) { error.value = data.error; return }
    result.value = data
  } catch (e) {
    error.value = `Could not run volatility research: ${e.message}`
  } finally {
    clearInterval(progressInterval)
    loading.value = false
    progress.value = ''
  }
}

async function runValidation() {
  if (!result.value) return
  loadingValidation.value = true
  error.value = ''

  try {
    const res  = await fetch(`${API_BASE}/api/volatility/validate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ symbols: ALL_SYMBOLS, fromYear: 2006, toYear: 2016 })
    })
    const data = await res.json()
    if (data.error) { error.value = data.error; return }
    validationResult.value = data
  } catch (e) {
    error.value = `Validation failed: ${e.message}`
  } finally {
    loadingValidation.value = false
  }
}

function primaryBucketRate(bucketName) {
  if (!result.value?.buckets) return 0
  return result.value.buckets.find(b => b.bucket === bucketName)?.beatRate ?? 0
}
</script>

<style scoped>
.vr-panel {
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
  color: var(--color-text);
}

/* ── Header ── */
.vr-header         { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.vr-header-actions { display: flex; gap: 6px; flex-wrap: wrap; }
.vr-title          { font-size: 14px; font-weight: 700; }

.vr-run-btn {
  padding: 8px 14px;
  border-radius: 7px;
  border: none;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.15s;
}
.vr-run-btn--secondary {
  background: var(--color-bg2);
  color: var(--color-text);
  border: 1px solid var(--color-border);
}
.vr-run-btn:disabled { opacity: 0.5; cursor: not-allowed; }

.vr-error {
  padding: 8px 10px;
  background: rgba(239,68,68,.12);
  color: var(--color-negative);
  border-radius: 6px;
  font-size: 12px;
}

/* ── Hypothesis ── */
.vr-hypothesis  { background: var(--color-bg2); border-radius: 7px; padding: 10px 12px; }
.vr-hyp-label   { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 4px; }
.vr-hyp-text    { font-size: 12px; line-height: 1.6; font-style: italic; }

/* ── Bucket cards ── */
.vr-buckets {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}
.vr-bucket-card {
  background: var(--color-bg2);
  border-radius: 9px;
  padding: 12px 14px;
  border-left: 3px solid var(--color-border);
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.card-beat    { border-left-color: var(--color-positive); }
.card-partial { border-left-color: var(--color-partial);  }
.card-miss    { border-left-color: var(--color-negative); }

.vr-bc-label     { font-size: 10px; font-weight: 700; color: var(--color-muted); margin-bottom: 4px; }
.vr-bc-beat      { font-size: 11px; color: var(--color-muted); }
.vr-bc-rate      { font-size: 22px; font-weight: 700; font-variant-numeric: tabular-nums; }
.vr-bc-adv-label { font-size: 9px; color: var(--color-muted); margin-top: 4px; }
.vr-bc-adv       { font-size: 13px; font-weight: 700; font-variant-numeric: tabular-nums; }
.vr-bc-vol-label { font-size: 9px; color: var(--color-muted); margin-top: 4px; }
.vr-bc-vol       { font-size: 12px; font-weight: 600; color: var(--color-muted); }

/* ── Finding ── */
.vr-finding {
  background: var(--color-bg2);
  border-left: 3px solid var(--color-accent);
  border-radius: 7px;
  padding: 10px 12px;
}
.vr-finding-label { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 5px; }
.vr-finding-text  { font-size: 12px; line-height: 1.6; }

/* ── Validation ── */
.vr-validation-wrap {
  background: var(--color-bg2);
  border: 1px solid rgba(234,179,8,.35);
  border-radius: 10px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.vr-val-header  { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.vr-val-title   { font-size: 13px; font-weight: 700; }
.vr-val-sub     { font-size: 11px; color: var(--color-muted); }

.vr-val-buckets {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}
.vr-val-card {
  background: var(--color-bg);
  border-radius: 7px;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.vr-val-bucket-label { font-size: 10px; font-weight: 700; color: var(--color-muted); }
.vr-val-row     { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.vr-val-period  { text-align: center; flex: 1; }
.vr-val-period-label { font-size: 9px; color: var(--color-muted); margin-bottom: 2px; }
.vr-val-beat    { font-size: 15px; font-weight: 700; font-variant-numeric: tabular-nums; }
.vr-val-arrow   { color: var(--color-muted); font-size: 12px; flex-shrink: 0; }
.vr-val-verdict { flex-shrink: 0; }
.vr-val-finding { margin-top: 2px; }

/* ── Table ── */
.vr-table-wrap { overflow-x: auto; }
.vr-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.vr-table th {
  text-align: left;
  padding: 6px 8px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-muted);
  border-bottom: 1px solid var(--color-border);
  white-space: nowrap;
}
.vr-table td { padding: 8px; border-bottom: 1px solid var(--color-border); vertical-align: middle; }
.row-beat { background: rgba(34,197,94,.04); }
.row-miss { background: transparent; }
.vr-table tbody tr:hover { background: var(--color-bg2); }

.td-symbol   { font-weight: 700; font-family: monospace; font-size: 12px; }
.td-bucket   { font-size: 11px; color: var(--color-muted); }
.td-vol      { font-size: 11px; font-variant-numeric: tabular-nums; }
.td-muted    { color: var(--color-muted); }
.td-positive { color: var(--color-positive); font-weight: 600; }
.td-negative { color: var(--color-negative); }

.verdict-chip  { font-size: 10px; font-weight: 600; padding: 2px 7px; border-radius: 4px; white-space: nowrap; }
.chip-beat     { background: rgba(34,197,94,.15); color: var(--color-positive); }
.chip-partial  { background: rgba(234,179,8,.15);  color: var(--color-partial);  }
.chip-miss     { background: rgba(239,68,68,.12);  color: var(--color-negative); }

/* ── Empty state ── */
.vr-empty {
  text-align: center;
  padding: 2.5rem 1rem;
  color: var(--color-muted);
  font-size: 13px;
  line-height: 1.7;
}
.vr-empty-detail { font-size: 11px; opacity: 0.7; }
</style>