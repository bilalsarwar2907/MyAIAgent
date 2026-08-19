<template>
  <div class="sc-panel">

    <!-- Header -->
    <div class="sc-header">
      <div class="sc-header-left">
        <span class="sc-title">🎯 RSI Candidate Screener</span>
        <span class="sc-subtitle">Stocks passing the Finding #1 exclusion rule · Sorted by current RSI (oversold first)</span>
      </div>
      <div class="sc-header-right">
        <span class="sc-run-hint">Run weekly · Tuesday recommended</span>
        <button class="sc-run-btn" :disabled="loading" @click="runScreener">
          <span v-if="loading" class="sc-spinner">⟳</span>
          <span v-else>▶ Run Screener</span>
        </button>
      </div>
    </div>

    <!-- Rule banner -->
    <div class="sc-rule-banner">
      <span class="sc-rule-icon">⚖️</span>
      <div>
        <span class="sc-rule-label">ACTIVE EXCLUSION RULE</span>
        <span class="sc-rule-text"> Finding #1 (Validated): Exclude stocks with &gt;300% 10-year buy &amp; hold return.
          Strong-trend stocks produced 0/22 RSI wins across two independent decades.</span>
      </div>
    </div>

    <!-- Error -->
    <div v-if="error" class="sc-error">⚠️ {{ error }}</div>

    <!-- Loading -->
    <div v-if="loading" class="sc-loading">
      <div class="sc-loading-spinner">⟳</div>
      <div class="sc-loading-text">Fetching 10-year price history for {{ totalSymbols }} stocks…</div>
      <div class="sc-loading-sub">Calculating B&amp;H returns and current RSI values</div>
    </div>

    <!-- Results -->
    <template v-if="result && !loading">

      <!-- Summary stats -->
      <div class="sc-stats-row">
        <div class="sc-stat-card">
          <div class="sc-stat-value">{{ result.totalScreened }}</div>
          <div class="sc-stat-label">Screened</div>
        </div>
        <div class="sc-stat-card sc-stat-pass">
          <div class="sc-stat-value sc-green">{{ result.totalCandidates }}</div>
          <div class="sc-stat-label">Candidates</div>
        </div>
        <div class="sc-stat-card sc-stat-fail">
          <div class="sc-stat-value sc-red">{{ result.totalExcluded }}</div>
          <div class="sc-stat-label">Excluded</div>
        </div>
        <div class="sc-stat-card" :class="{ 'sc-stat-card--active': result.oversoldCount > 0 }">
          <div class="sc-stat-value" :class="result.oversoldCount > 0 ? 'sc-red' : 'sc-muted'">{{ result.oversoldCount }}</div>
          <div class="sc-stat-label">Entry Signal (RSI &lt;30 ↑)</div>
        </div>
        <div class="sc-stat-card" :class="{ 'sc-stat-card--experimental': result.experimentalCount > 0 }">
          <div class="sc-stat-value" :class="result.experimentalCount > 0 ? 'sc-purple' : 'sc-muted'">{{ result.experimentalCount }}</div>
          <div class="sc-stat-label">Experimental (RSI 30–40 ↑)</div>
        </div>
        <div class="sc-stat-card sc-stat-time">
          <div class="sc-stat-value sc-muted sc-stat-time-val">{{ formattedTime }}</div>
          <div class="sc-stat-label">Generated</div>
        </div>
      </div>

      <!-- Banner: Track A entry signal (RSI < 30) -->
      <div v-if="result.oversoldCount > 0" class="sc-action-banner sc-action-banner--signal">
        <span class="sc-action-icon">🔴</span>
        <div>
          <span class="sc-action-title">{{ result.oversoldCount }} entry signal{{ result.oversoldCount > 1 ? 's' : '' }} — RSI &lt; 30, turning up. Track A (validated rule).</span>
          <span class="sc-action-text"> Review against the rulebook before opening a paper trade.</span>
        </div>
      </div>

      <!-- Banner: Track B experimental (RSI 30-40 + slope up) -->
      <div v-else-if="result.experimentalCount > 0" class="sc-action-banner sc-action-banner--experimental">
        <span class="sc-action-icon">🧪</span>
        <div>
          <span class="sc-action-title">{{ result.experimentalCount }} experimental signal{{ result.experimentalCount > 1 ? 's' : '' }} — RSI 30–40, turning up. Track B (not yet validated).</span>
          <span class="sc-action-text"> Do not mix results with Track A. Log separately if you choose to trade.</span>
        </div>
      </div>

      <!-- Banner: Watching only -->
      <div v-else-if="watchingCount > 0" class="sc-action-banner sc-action-banner--watching">
        <span class="sc-action-icon">🟡</span>
        <div>
          <span class="sc-action-title">{{ watchingCount }} stock{{ watchingCount > 1 ? 's' : '' }} in the watching zone (RSI &lt; 40, still falling).</span>
          <span class="sc-action-text"> No entry yet — wait for RSI to turn up.</span>
        </div>
      </div>

      <!-- Banner: nothing -->
      <div v-else class="sc-action-banner sc-action-banner--nosignal">
        <span class="sc-action-icon">⚪</span>
        <div>
          <span class="sc-action-title">No setups today.</span>
          <span class="sc-action-text"> {{ result.totalCandidates }} stocks passed validation — none in the RSI &lt; 40 zone. Check back tomorrow.</span>
        </div>
      </div>

      <!-- Filters -->
      <div class="sc-filters">
        <div class="sc-filter-group">
          <label class="sc-filter-label">SECTOR</label>
          <select class="sc-select" v-model="filterSector">
            <option value="">All sectors</option>
            <option v-for="s in availableSectors" :key="s" :value="s">{{ s }}</option>
          </select>
        </div>
        <div class="sc-filter-group">
          <label class="sc-filter-label">TREND BUCKET</label>
          <select class="sc-select" v-model="filterBucket">
            <option value="">All buckets</option>
            <option value="Weak (<100%)">Weak (&lt;100%)</option>
            <option value="Medium (100–300%)">Medium (100–300%)</option>
          </select>
        </div>
        <div class="sc-filter-group">
          <label class="sc-filter-label">RSI SIGNAL</label>
          <select class="sc-select" v-model="filterSignal">
            <option value="">All</option>
            <option value="Entry Signal">🔴 Entry Signal (RSI &lt;30)</option>
            <option value="Experimental">🧪 Experimental (RSI 30–40)</option>
            <option value="Watching">🟡 Watching</option>
            <option value="No Setup">⚪ No Setup</option>
          </select>
        </div>
        <div class="sc-filter-group sc-filter-search">
          <label class="sc-filter-label">SEARCH</label>
          <input class="sc-input" v-model="filterSearch" placeholder="Symbol…" />
        </div>
      </div>

      <!-- Candidates table -->
      <div class="sc-table-wrap">
        <table class="sc-table">
          <thead>
            <tr>
              <th @click="setSort('symbol')" class="sc-th sc-th-sortable">
                Symbol <span class="sc-sort-icon">{{ sortIcon('symbol') }}</span>
              </th>
              <th @click="setSort('sector')" class="sc-th sc-th-sortable">
                Sector <span class="sc-sort-icon">{{ sortIcon('sector') }}</span>
              </th>
              <th @click="setSort('bahReturn')" class="sc-th sc-th-sortable sc-th-num">
                10y Return <span class="sc-sort-icon">{{ sortIcon('bahReturn') }}</span>
              </th>
              <th @click="setSort('trendBucket')" class="sc-th sc-th-sortable">
                Trend Bucket <span class="sc-sort-icon">{{ sortIcon('trendBucket') }}</span>
              </th>
              <th @click="setSort('currentRsi')" class="sc-th sc-th-sortable sc-th-num">
                Current RSI <span class="sc-sort-icon">{{ sortIcon('currentRsi') }}</span>
              </th>
              <th class="sc-th sc-th-signal">Signal</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredCandidates.length === 0">
              <td colspan="6" class="sc-empty">No candidates match the current filters.</td>
            </tr>
            <!-- FIX #3 — oversold rows get strong highlight; sc-row--oversold already exists, strengthen it -->
            <tr
              v-for="s in filteredCandidates"
              :key="s.symbol"
              class="sc-row"
              :class="{
                'sc-row--entry': s.signalStatus === 'Entry Signal',
                'sc-row--experimental': s.signalStatus === 'Experimental',
                'sc-row--watching': s.signalStatus === 'Watching'
              }"
            >
              <td class="sc-td sc-td-symbol">
                <span v-if="s.signalStatus === 'Entry Signal'" class="sc-row-signal-dot sc-row-signal-dot--entry">●</span>
                <span v-else-if="s.signalStatus === 'Experimental'" class="sc-row-signal-dot sc-row-signal-dot--experimental">●</span>
                <span v-else-if="s.signalStatus === 'Watching'" class="sc-row-signal-dot sc-row-signal-dot--watching">●</span>
                {{ s.symbol }}
              </td>
              <td class="sc-td sc-td-sector">{{ s.sector }}</td>
              <td class="sc-td sc-td-num">
                <span :class="bahClass(s.bahReturn)">{{ s.bahReturn }}%</span>
              </td>
              <td class="sc-td">
                <span class="sc-bucket-badge" :class="bucketClass(s.trendBucket)">{{ s.trendBucket }}</span>
              </td>
              <td class="sc-td sc-td-num">
                <span v-if="s.currentRsi !== null" class="sc-rsi" :class="rsiClass(s.currentRsi)">
                  {{ s.currentRsi }}
                </span>
                <span v-else class="sc-rsi-na">—</span>
              </td>
              <td class="sc-td sc-td-signal">
                <span v-if="s.signalStatus === 'Entry Signal'" class="sc-signal sc-signal--entry">
                  🔴 Entry Signal
                </span>
                <span v-else-if="s.signalStatus === 'Experimental'" class="sc-signal sc-signal--experimental">
                  🧪 Experimental
                </span>
                <span v-else-if="s.signalStatus === 'Watching'" class="sc-signal sc-signal--watching">
                  🟡 Watching
                </span>
                <span v-else class="sc-signal sc-signal--neutral">
                  ⚪ No Setup
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Excluded stocks (collapsible) -->
      <div class="sc-excluded-section">
        <button class="sc-excluded-toggle" @click="showExcluded = !showExcluded">
          {{ showExcluded ? '▲' : '▼' }} {{ result.totalExcluded }} excluded stocks (strong-trend rule)
        </button>
        <div v-if="showExcluded" class="sc-excluded-table-wrap">
          <table class="sc-table sc-table--excluded">
            <thead>
              <tr>
                <th class="sc-th">Symbol</th>
                <th class="sc-th">Sector</th>
                <th class="sc-th sc-th-num">10y Return</th>
                <th class="sc-th">Reason</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in result.excluded" :key="s.symbol" class="sc-row sc-row--excluded">
                <td class="sc-td sc-td-symbol">{{ s.symbol }}</td>
                <td class="sc-td sc-td-sector">{{ s.sector }}</td>
                <td class="sc-td sc-td-num sc-red">{{ s.bahReturn }}%</td>
                <td class="sc-td sc-td-reason">{{ s.excludeReason }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Errors (if any) -->
      <div v-if="result.errors && result.errors.length" class="sc-errors-section">
        <div class="sc-errors-label">⚠️ {{ result.errors.length }} symbol(s) failed to load</div>
        <div v-for="e in result.errors" :key="e" class="sc-error-item">{{ e }}</div>
      </div>

    </template>

    <!-- FIX #4 — Empty state: before first run -->
    <div v-if="!result && !loading" class="sc-empty-state">
      <div class="sc-empty-icon">🎯</div>
      <div class="sc-empty-title">RSI Candidate Screener</div>
      <div class="sc-empty-desc">
        Click <strong>Run Screener</strong> to fetch current RSI values for all 59 stocks and apply
        the validated Finding #1 exclusion rule. Takes ~30–60 seconds.
      </div>
      <div class="sc-empty-hint-row">
        <div class="sc-empty-hint-card">
          <div class="sc-empty-hint-label">What it checks</div>
          <div class="sc-empty-hint-text">RSI &lt; 30 · 10-year return &lt; 300% · Finding #1 exclusion rule</div>
        </div>
        <div class="sc-empty-hint-card">
          <div class="sc-empty-hint-label">When to run</div>
          <div class="sc-empty-hint-text">Once per week · Tuesday recommended · Markets open</div>
        </div>
        <div class="sc-empty-hint-card">
          <div class="sc-empty-hint-label">What to do with results</div>
          <div class="sc-empty-hint-text">Oversold candidates → validate in Research → open Paper Trade</div>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const BASE = 'http://localhost:60363'

const result       = ref(null)
const loading      = ref(false)
const error        = ref(null)
const showExcluded = ref(false)
const totalSymbols = 59

// Filters
const filterSector  = ref('')
const filterBucket  = ref('')
const filterSignal  = ref('')
const filterSearch  = ref('')

// Sort
const sortKey = ref('currentRsi')
const sortAsc = ref(true)

async function runScreener() {
  loading.value = true
  error.value   = null
  result.value  = null
  try {
    const res = await fetch(`${BASE}/api/screener/rsi-candidates`)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    result.value = await res.json()
  } catch (e) {
    error.value = 'Failed to run screener: ' + e.message
  } finally {
    loading.value = false
  }
}

const formattedTime = computed(() => {
  if (!result.value?.generatedAt) return '—'
  const d = new Date(result.value.generatedAt)
  const day   = d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
  const time  = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
  return `${day} · ${time}`
})

const availableSectors = computed(() => {
  if (!result.value) return []
  return [...new Set(result.value.candidates.map(s => s.sector))].sort()
})

const filteredCandidates = computed(() => {
  if (!result.value) return []
  let list = [...result.value.candidates]

  if (filterSector.value)
    list = list.filter(s => s.sector === filterSector.value)

  if (filterBucket.value)
    list = list.filter(s => s.trendBucket === filterBucket.value)

  if (filterSignal.value)
    list = list.filter(s => s.signalStatus === filterSignal.value)

  if (filterSearch.value.trim())
    list = list.filter(s => s.symbol.includes(filterSearch.value.trim().toUpperCase()))

  // Sort
  list.sort((a, b) => {
    let va = a[sortKey.value]
    let vb = b[sortKey.value]
    if (va === null || va === undefined) va = sortAsc.value ? Infinity : -Infinity
    if (vb === null || vb === undefined) vb = sortAsc.value ? Infinity : -Infinity
    if (typeof va === 'string') return sortAsc.value ? va.localeCompare(vb) : vb.localeCompare(va)
    return sortAsc.value ? va - vb : vb - va
  })

  return list
})

function setSort(key) {
  if (sortKey.value === key) sortAsc.value = !sortAsc.value
  else { sortKey.value = key; sortAsc.value = true }
}

function sortIcon(key) {
  if (sortKey.value !== key) return '↕'
  return sortAsc.value ? '↑' : '↓'
}

const watchingCount = computed(() =>
  result.value ? result.value.candidates.filter(s => s.signalStatus === 'Watching').length : 0
)

function rsiClass(rsi) {
  if (rsi < 40) return 'rsi--oversold'
  if (rsi > 70) return 'rsi--overbought'
  return 'rsi--neutral'
}

function bahClass(val) {
  if (val >= 200) return 'sc-green'
  if (val >= 0)   return ''
  return 'sc-red'
}

function bucketClass(bucket) {
  if (bucket.startsWith('Weak'))   return 'bucket--weak'
  if (bucket.startsWith('Medium')) return 'bucket--medium'
  return 'bucket--strong'
}
</script>

<style scoped>
.sc-panel {
  --sc-bg:      var(--bg-panel);
  --sc-bg2:     var(--bg-panel-item);
  --sc-border:  var(--bg-panel-border);
  --sc-text:    var(--text-primary);
  --sc-muted:   var(--text-secondary);
  --sc-accent:  var(--accent);
  --sc-green:   #22c55e;
  --sc-red:     #ef4444;
  --sc-yellow:  #eab308;
  --sc-purple:  #a855f7;

  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 1rem;
  min-width: 0;
  background: var(--sc-bg);
  color: var(--sc-text);
}

/* ── Header ── */
.sc-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.sc-header-left  { display: flex; flex-direction: column; gap: 3px; }
.sc-header-right { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; flex-shrink: 0; }
.sc-title        { font-size: 15px; font-weight: 700; }
.sc-subtitle     { font-size: 11px; color: var(--sc-muted); }
.sc-run-hint     { font-size: 10px; color: var(--sc-muted); }

.sc-run-btn {
  background: var(--sc-accent);
  color: #fff;
  border: none;
  border-radius: 7px;
  padding: 8px 18px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  white-space: nowrap;
  transition: opacity 0.15s;
}
.sc-run-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.sc-run-btn:hover:not(:disabled) { opacity: 0.85; }

.sc-spinner { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* ── Rule banner ── */
.sc-rule-banner {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  background: var(--sc-bg2);
  border: 1px solid var(--sc-border);
  border-left: 3px solid var(--sc-green);
  border-radius: 7px;
  padding: 10px 12px;
  font-size: 12px;
  line-height: 1.5;
}
.sc-rule-icon  { font-size: 14px; flex-shrink: 0; margin-top: 1px; }
.sc-rule-label { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--sc-green); margin-right: 4px; }
.sc-rule-text  { color: var(--sc-muted); }

/* ── Error ── */
.sc-error {
  background: rgba(239,68,68,.1);
  border: 1px solid rgba(239,68,68,.3);
  border-radius: 7px;
  padding: 10px 14px;
  font-size: 12px;
  color: var(--sc-red);
}

/* ── Loading ── */
.sc-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 40px 20px;
  color: var(--sc-muted);
}
.sc-loading-spinner {
  font-size: 28px;
  animation: spin 1s linear infinite;
  color: var(--sc-accent);
}
.sc-loading-text { font-size: 13px; font-weight: 600; color: var(--sc-text); }
.sc-loading-sub  { font-size: 11px; }

/* ── Stats row ── */
.sc-stats-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.sc-stat-card {
  background: var(--sc-bg2);
  border: 1px solid var(--sc-border);
  border-radius: 8px;
  padding: 8px 14px;
  text-align: center;
  min-width: 72px;
  flex: 1;
  transition: border-color 0.2s;
}
.sc-stat-card--active {
  border-color: var(--sc-green);
  box-shadow: 0 0 0 1px rgba(34,197,94,.2);
}
.sc-stat-value { font-size: 20px; font-weight: 700; font-variant-numeric: tabular-nums; }
.sc-stat-label { font-size: 9px; color: var(--sc-muted); text-transform: uppercase; letter-spacing: 0.06em; margin-top: 2px; }
.sc-stat-time-val { font-size: 11px; }
.sc-green  { color: var(--sc-green); }
.sc-red    { color: var(--sc-red); }
.sc-purple { color: var(--sc-purple); }
.sc-muted  { color: var(--sc-muted); }
.sc-accent { color: var(--sc-accent); }

.sc-stat-card--experimental {
  border-color: var(--sc-purple);
  box-shadow: 0 0 0 1px rgba(168,85,247,.2);
}

/* ── FIX #2 — Action banner ── */
.sc-action-banner {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  border-radius: 7px;
  padding: 10px 14px;
  font-size: 12px;
  line-height: 1.5;
  border: 1px solid;
}
.sc-action-banner--signal {
  background: rgba(34,197,94,.07);
  border-color: rgba(34,197,94,.3);
}
.sc-action-banner--experimental {
  background: rgba(168,85,247,.06);
  border-color: rgba(168,85,247,.3);
}
.sc-action-banner--watching {
  background: rgba(234,179,8,.06);
  border-color: rgba(234,179,8,.3);
}
.sc-action-banner--nosignal {
  background: var(--sc-bg2);
  border-color: var(--sc-border);
}
.sc-action-icon  { font-size: 14px; flex-shrink: 0; margin-top: 1px; }
.sc-action-title { font-weight: 700; }
.sc-action-text  { color: var(--sc-muted); }

/* ── Filters ── */
.sc-filters {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: flex-end;
}
.sc-filter-group  { display: flex; flex-direction: column; gap: 4px; }
.sc-filter-search { flex: 1; min-width: 120px; }
.sc-filter-label {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--sc-muted);
}
.sc-select, .sc-input {
  background: var(--sc-bg2);
  border: 1px solid var(--sc-border);
  border-radius: 6px;
  padding: 5px 8px;
  font-size: 12px;
  color: var(--sc-text);
  height: 30px;
}
.sc-input { min-width: 100px; }
.sc-select:focus, .sc-input:focus {
  outline: none;
  border-color: var(--sc-accent);
}

/* ── Table ── */
.sc-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--sc-border);
  border-radius: 8px;
}
.sc-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.sc-th {
  background: var(--sc-bg2);
  padding: 9px 12px;
  text-align: left;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--sc-muted);
  border-bottom: 1px solid var(--sc-border);
  white-space: nowrap;
  user-select: none;
}
.sc-th-num    { text-align: right; }
.sc-th-signal { text-align: center; }
.sc-th-sortable { cursor: pointer; }
.sc-th-sortable:hover { color: var(--sc-text); }
.sc-sort-icon { font-size: 10px; opacity: 0.6; }

.sc-td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--sc-border);
  vertical-align: middle;
}
.sc-td-num    { text-align: right; font-variant-numeric: tabular-nums; }
.sc-td-signal { text-align: center; }
.sc-td-symbol { font-weight: 700; font-size: 13px; }
.sc-td-sector { color: var(--sc-muted); font-size: 11px; text-transform: capitalize; }
.sc-td-reason { font-size: 10px; color: var(--sc-muted); }

.sc-row:hover { background: var(--sc-bg2); }

.sc-row--entry {
  background: rgba(239,68,68,.06);
  border-left: 3px solid var(--sc-red);
}
.sc-row--entry:hover { background: rgba(239,68,68,.10); }

.sc-row--experimental {
  background: rgba(168,85,247,.06);
  border-left: 3px solid #a855f7;
}
.sc-row--experimental:hover { background: rgba(168,85,247,.10); }

.sc-row--watching {
  background: rgba(234,179,8,.05);
  border-left: 3px solid var(--sc-yellow);
}
.sc-row--watching:hover { background: rgba(234,179,8,.09); }

.sc-row--excluded { opacity: 0.7; }

.sc-row-signal-dot {
  font-size: 8px;
  margin-right: 4px;
  vertical-align: middle;
}
.sc-row-signal-dot--entry        { color: var(--sc-red); }
.sc-row-signal-dot--experimental { color: #a855f7; }
.sc-row-signal-dot--watching     { color: var(--sc-yellow); }

.sc-empty { text-align: center; padding: 24px; color: var(--sc-muted); font-size: 12px; }

/* RSI value colours */
.sc-rsi { font-weight: 700; font-variant-numeric: tabular-nums; }
.rsi--oversold   { color: var(--sc-green); }
.rsi--overbought { color: var(--sc-red); }
.rsi--neutral    { color: var(--sc-text); }
.sc-rsi-na       { color: var(--sc-muted); }

/* Bucket badges */
.sc-bucket-badge {
  font-size: 10px;
  font-weight: 600;
  padding: 2px 7px;
  border-radius: 4px;
  white-space: nowrap;
}
.bucket--weak   { background: rgba(34,197,94,.12);  color: var(--sc-green); }
.bucket--medium { background: rgba(234,179,8,.12);  color: var(--sc-yellow); }
.bucket--strong { background: rgba(239,68,68,.12);  color: var(--sc-red); }

/* Signal chips */
.sc-signal {
  font-size: 10px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 4px;
  white-space: nowrap;
}
.sc-signal--entry        { background: rgba(239,68,68,.12);  color: var(--sc-red); }
.sc-signal--experimental { background: rgba(168,85,247,.12); color: #a855f7; }
.sc-signal--watching     { background: rgba(234,179,8,.12);  color: var(--sc-yellow); }
.sc-signal--neutral      { background: var(--sc-bg2); color: var(--sc-muted); }

/* ── Excluded section ── */
.sc-excluded-section { display: flex; flex-direction: column; gap: 8px; }
.sc-excluded-toggle {
  background: none;
  border: 1px solid var(--sc-border);
  border-radius: 6px;
  padding: 7px 12px;
  font-size: 11px;
  color: var(--sc-muted);
  cursor: pointer;
  text-align: left;
  width: 100%;
}
.sc-excluded-toggle:hover { color: var(--sc-text); background: var(--sc-bg2); }
.sc-excluded-table-wrap {
  border: 1px solid var(--sc-border);
  border-radius: 8px;
  overflow-x: auto;
}
.sc-table--excluded { opacity: 0.75; }

/* ── Errors ── */
.sc-errors-section {
  background: rgba(239,68,68,.06);
  border: 1px solid rgba(239,68,68,.2);
  border-radius: 7px;
  padding: 10px 14px;
  font-size: 11px;
}
.sc-errors-label { font-weight: 700; color: var(--sc-red); margin-bottom: 6px; }
.sc-error-item   { color: var(--sc-muted); padding: 2px 0; }

/* ── FIX #4 — Empty state (before first run) ── */
.sc-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 48px 24px;
  text-align: center;
}
.sc-empty-icon  { font-size: 36px; opacity: 0.4; }
.sc-empty-title { font-size: 14px; font-weight: 700; }
.sc-empty-desc  { font-size: 12px; color: var(--sc-muted); line-height: 1.6; max-width: 420px; }

.sc-empty-hint-row {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  justify-content: center;
  max-width: 580px;
  margin-top: 4px;
}
.sc-empty-hint-card {
  background: var(--sc-bg2);
  border: 1px solid var(--sc-border);
  border-radius: 8px;
  padding: 10px 14px;
  text-align: left;
  min-width: 160px;
  flex: 1;
}
.sc-empty-hint-label {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--sc-muted);
  text-transform: uppercase;
  margin-bottom: 4px;
}
.sc-empty-hint-text {
  font-size: 11px;
  color: var(--sc-text);
  line-height: 1.5;
}
</style>