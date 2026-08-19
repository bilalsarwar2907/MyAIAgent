<template>
  <div class="sr-panel">

    <div class="sr-header">
      <span class="sr-title">Sector Research</span>
      <div class="sr-header-actions">
        <button class="sr-run-btn" :disabled="loading" @click="runAllSectors">
          {{ loading ? `Running ${progress}…` : 'Run All Sectors (2016–2026)' }}
        </button>
        <button
          class="sr-run-btn sr-run-btn--secondary"
          :disabled="loadingPeriod || !sectors.length"
          @click="runPeriodValidation"
        >
          {{ loadingPeriod ? 'Running 2006–2016…' : 'Validate 2006–2016' }}
        </button>
      </div>
    </div>

    <div v-if="error" class="sr-error">{{ error }}</div>

    <!-- CHANGE 1: Research history banner — shown once data is loaded -->
    <div v-if="sectors.length" class="sr-history-banner">
      📋 <strong>Research History</strong> — This tab documents the sector research that led to the
      <strong>Findings Library</strong>. It is evidence of how the strategy was developed,
      not a tool for making trading decisions.
    </div>

    <!-- Market-level summary -->
    <div v-if="sectors.length" class="sr-market-summary">
      <div class="sr-ms-stat">
        <div class="sr-ms-label">Sectors tested</div>
        <div class="sr-ms-value">{{ sectors.length }}</div>
      </div>
      <div class="sr-ms-stat sr-ms-primary">
        <div class="sr-ms-label">Sectors beating B&H</div>
        <div class="sr-ms-value">{{ sectorsBeat }}/{{ sectors.length }}</div>
      </div>
      <div class="sr-ms-stat">
        <div class="sr-ms-label">Best sector</div>
        <div class="sr-ms-value sr-ms-highlight">{{ bestSector?.sector ?? '—' }}</div>
      </div>
      <div class="sr-ms-stat">
        <div class="sr-ms-label">Worst sector</div>
        <div class="sr-ms-value sr-ms-dim">{{ worstSector?.sector ?? '—' }}</div>
      </div>
    </div>

    <!-- Overall Market Result -->
    <div v-if="sectors.length" class="sr-market-verdict" :class="marketVerdict.cssClass">
      <div class="sr-mv-left">
        <div class="sr-mv-label">OVERALL MARKET RESULT</div>
        <div class="sr-mv-headline">{{ marketVerdict.icon }} {{ marketVerdict.text }}</div>
        <div class="sr-mv-sub">
          {{ totalBeat }} / {{ totalStocks }} stocks beat buy-and-hold
          ({{ ((totalBeat / Math.max(totalStocks,1)) * 100).toFixed(0) }}%)
        </div>
      </div>
      <div class="sr-mv-stats">
        <div class="sr-mv-stat">
          <div class="sr-mv-stat-label">Median advantage</div>
          <div class="sr-mv-stat-value" :class="overallMedianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
            {{ overallMedianAdvantage >= 0 ? '+' : '' }}{{ overallMedianAdvantage.toFixed(1) }}%
          </div>
        </div>
        <div class="sr-mv-stat">
          <div class="sr-mv-stat-label">Best sector</div>
          <div class="sr-mv-stat-value td-positive">{{ bestSector?.sector ?? '—' }}</div>
        </div>
        <div class="sr-mv-stat">
          <div class="sr-mv-stat-label">Worst sector</div>
          <div class="sr-mv-stat-value td-negative">{{ worstSector?.sector ?? '—' }}</div>
        </div>
        <div class="sr-mv-stat">
          <div class="sr-mv-stat-label">Stocks tested</div>
          <div class="sr-mv-stat-value">{{ totalStocks }}</div>
        </div>
      </div>
    </div>

    <!-- Market Research Report -->
    <div v-if="sectors.length" class="srr-wrap">

      <div class="srr-header">
        <span class="srr-title">📋 Market Research Report</span>
        <span class="srr-period">Period: 2016–2026 · {{ totalStocks }} stocks · {{ sectors.length }} sectors</span>
      </div>

      <div class="srr-finding">
        <div class="srr-finding-label">KEY FINDING</div>
        <div class="srr-finding-text">
          RSI mean-reversion strategies beat buy-and-hold in
          <strong>{{ totalBeat }}/{{ totalStocks }} stocks ({{ ((totalBeat/Math.max(totalStocks,1))*100).toFixed(0) }}%)</strong>
          — below the 50% threshold needed to claim a reliable edge.
          Median advantage across all stocks: <strong :class="overallMedianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
            {{ overallMedianAdvantage >= 0 ? '+' : '' }}{{ overallMedianAdvantage.toFixed(1) }}%</strong>.
        </div>
      </div>

      <div class="srr-cols">
        <div class="srr-col srr-col--win">
          <div class="srr-col-label">🟢 Where RSI worked</div>
          <div v-for="s in winningSectors" :key="s.sector" class="srr-sector-row">
            <span class="srr-sector-name">{{ s.sector }}</span>
            <span class="srr-sector-beat">{{ s.beatCount }}/{{ s.symbolsTested }}</span>
            <span class="td-positive srr-sector-adv">+{{ s.medianAdvantage.toFixed(1) }}%</span>
          </div>
          <div v-if="!winningSectors.length" class="srr-none">None outperformed</div>
        </div>
        <div class="srr-col srr-col--loss">
          <div class="srr-col-label">🔴 Where RSI failed</div>
          <div v-for="s in losingSectors" :key="s.sector" class="srr-sector-row">
            <span class="srr-sector-name">{{ s.sector }}</span>
            <span class="srr-sector-beat">{{ s.beatCount }}/{{ s.symbolsTested }}</span>
            <span class="td-negative srr-sector-adv">{{ s.medianAdvantage.toFixed(1) }}%</span>
          </div>
        </div>
      </div>

      <div class="srr-hypotheses">
        <div class="srr-hyp-label">HYPOTHESES TESTED</div>
        <div class="srr-hyp-row">
          <span class="srr-hyp-verdict srr-hyp-rejected">❌ Rejected</span>
          <span class="srr-hyp-text">RSI has a reliable universal edge across all stocks</span>
        </div>
        <div class="srr-hyp-row">
          <span class="srr-hyp-verdict srr-hyp-rejected">❌ Rejected</span>
          <span class="srr-hyp-text">200-day MA filter consistently improves performance</span>
        </div>
        <!-- CHANGE: Nuanced hypothesis label — partial, not fully supported -->
        <div class="srr-hyp-row">
          <span class="srr-hyp-verdict srr-hyp-partial">⚠️ Partial</span>
          <span class="srr-hyp-text">
            Sector classification predicts RSI effectiveness —
            Energy showed cross-period persistence, but Airlines did not hold up in 2006–2016.
            Sector alone is not a reliable signal.
          </span>
        </div>
      </div>

      <div class="srr-caveats">
        ⚠️ Survivorship bias · Single time window · No transaction costs · Small trade counts per stock · Historical simulation only
      </div>
    </div>

    <!-- Multi-Period Validation Report -->
    <div v-if="periodData.length" class="srr-wrap srr-wrap--period">

      <div class="srr-header">
        <span class="srr-title">🔬 Multi-Period Validation</span>
        <span class="srr-period">Comparing 2016–2026 vs 2006–2016</span>
      </div>

      <div class="srr-finding">
        <div class="srr-finding-label">VALIDATION QUESTION</div>
        <div class="srr-finding-text">
          Do the sectors that outperformed in 2016–2026 also outperform in 2006–2016?
          A pattern that survives both periods is significantly more reliable.
        </div>
      </div>

      <div class="srr-period-grid">
        <div
          v-for="row in periodComparison"
          :key="row.sector"
          class="srr-period-card"
          :class="row.heldUp === 'Yes' ? 'card-beat' : row.heldUp === 'Partial' ? 'card-partial' : 'card-miss'"
        >
          <div class="srr-pc-top">
            <span class="srr-pc-sector">{{ row.sector }}</span>
            <span class="verdict-chip"
              :class="row.heldUp === 'Yes' ? 'chip-beat' : row.heldUp === 'Partial' ? 'chip-partial' : 'chip-miss'">
              {{ row.heldUp === 'Yes' ? '✅ Held up' : row.heldUp === 'Partial' ? '⚠️ Partial' : '❌ Failed' }}
            </span>
          </div>
          <div class="srr-pc-row">
            <div class="srr-pc-period">
              <div class="srr-pc-period-label">2016–2026</div>
              <div class="srr-pc-beat">{{ row.beat2016_2026 }}/{{ row.tested2016_2026 }}</div>
              <div :class="row.median2016_2026 >= 0 ? 'td-positive' : 'td-negative'" class="srr-pc-adv">
                {{ row.median2016_2026 >= 0 ? '+' : '' }}{{ row.median2016_2026.toFixed(0) }}%
              </div>
            </div>
            <div class="srr-pc-divider">→</div>
            <div class="srr-pc-period">
              <div class="srr-pc-period-label">2006–2016</div>
              <div class="srr-pc-beat">{{ row.beat2006_2016 }}/{{ row.tested2006_2016 }}</div>
              <div :class="row.median2006_2016 >= 0 ? 'td-positive' : 'td-negative'" class="srr-pc-adv">
                {{ row.median2006_2016 >= 0 ? '+' : '' }}{{ row.median2006_2016.toFixed(0) }}%
              </div>
            </div>
          </div>
          <div class="srr-pc-score">
            <div class="srr-pc-score-bar-wrap">
              <div class="srr-pc-score-bar"
                :class="scoreColor(confidenceScore(row))"
                :style="{ width: confidenceScore(row) + '%' }">
              </div>
            </div>
            <span class="srr-pc-score-label" :class="scoreColor(confidenceScore(row))">
              {{ confidenceScore(row) }}/100
            </span>
          </div>
        </div>
      </div>

      <div class="srr-finding srr-finding--period">
        <div class="srr-finding-label">VALIDATION FINDING</div>
        <div class="srr-finding-text">{{ periodFinding }}</div>
      </div>

      <div class="srr-caveats">
        ⚠️ 2006–2016 includes the 2008 financial crisis and 2014–2016 oil price collapse — a significantly different market regime.
      </div>
    </div>

    <!-- Sector table -->
    <div v-if="sectors.length" class="sr-table-wrap">
      <table class="sr-table">
        <thead>
          <tr>
            <th>Sector</th>
            <th>Stocks</th>
            <th>Beat B&H</th>
            <th>Median Advantage</th>
            <th>Best Stock</th>
            <th>Worst Stock</th>
            <th>Verdict</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in sectors" :key="s.sector"
              :class="s.verdict === 'Outperformed' ? 'row-beat' : 'row-miss'">
            <td class="td-sector">{{ s.sector }}</td>
            <td>{{ s.symbolsTested }}</td>
            <td>{{ s.beatCount }}/{{ s.symbolsTested }}</td>
            <td :class="s.medianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
              {{ s.medianAdvantage >= 0 ? '+' : '' }}{{ s.medianAdvantage.toFixed(1) }}%
            </td>
            <td class="td-small">
              <span class="td-symbol">{{ s.bestSymbol }}</span>
              <span class="td-positive"> +{{ s.bestAdvantage.toFixed(1) }}%</span>
            </td>
            <td class="td-small">
              <span class="td-symbol">{{ s.worstSymbol }}</span>
              <span class="td-negative"> {{ s.worstAdvantage.toFixed(1) }}%</span>
            </td>
            <td>
              <span class="verdict-chip" :class="s.verdict === 'Outperformed' ? 'chip-beat' : 'chip-miss'">
                {{ s.verdict === 'Outperformed' ? '🟢' : '🔴' }} {{ s.verdict }}
              </span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Per-sector drill-down -->
    <div v-if="sectors.length" class="sr-drilldown">
      <div class="sr-drilldown-label">Drill into a sector</div>
      <div class="sr-drilldown-row">
        <select v-model="selectedSector" class="sr-select">
          <option value="">Select sector…</option>
          <option v-for="s in sectors" :key="s.sector" :value="s.sector">
            {{ s.sector }}
          </option>
        </select>
        <button class="sr-drill-btn" :disabled="loadingDrill || !selectedSector" @click="drillDown">
          {{ loadingDrill ? 'Loading…' : 'View Stocks' }}
        </button>
      </div>

      <div v-if="drillResult" class="sr-drill-table-wrap">
        <div class="sr-drill-header">
          {{ drillResult.sector }} — {{ drillResult.beatCount }}/{{ drillResult.symbolsTested }} beat B&H
          · Median advantage:
          <span :class="drillResult.medianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
            {{ drillResult.medianAdvantage >= 0 ? '+' : '' }}{{ drillResult.medianAdvantage.toFixed(1) }}%
          </span>
        </div>
        <table class="sr-table">
          <thead>
            <tr>
              <th>Stock</th>
              <th>B&H</th>
              <th>Strategy</th>
              <th>Advantage</th>
              <th>Trades</th>
              <th>Win %</th>
              <th>Max DD</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in drillResult.perSymbol" :key="r.symbol"
                :class="r.beat ? 'row-beat' : 'row-miss'">
              <td class="td-symbol">{{ r.symbol }}</td>
              <td>{{ r.bahReturn.toFixed(1) }}%</td>
              <td class="td-small">{{ r.bestStrategy }}</td>
              <td :class="r.advantage >= 0 ? 'td-positive' : 'td-negative'">
                {{ r.advantage >= 0 ? '+' : '' }}{{ r.advantage.toFixed(1) }}%
              </td>
              <td>{{ r.trades }}</td>
              <td>{{ r.winRate }}%</td>
              <td class="td-negative">{{ r.maxDrawdown.toFixed(1) }}%</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- CHANGE 2+3: Final conclusion banner + link to Findings -->
    <div v-if="sectors.length" class="sr-conclusion-banner">
      <div class="sr-conclusion-title">📌 Research Conclusion</div>
      <div class="sr-conclusion-text">
        Sector alone is not a reliable trading signal. Energy showed persistence across
        periods but evidence remains limited (Finding #3 — partial). All other sectors
        failed cross-period validation. Trend strength (Finding #1) explains more variance
        than sector classification.
      </div>
      <div class="sr-conclusion-action">
        For actionable rules →
        <button class="sr-findings-link" @click="goToFindings">
          View Findings Library →
        </button>
      </div>
    </div>

    <div v-if="!sectors.length && !loading && !error" class="sr-empty">
      Click "Run All Sectors" to research how RSI strategies performed across
      10 sectors and 60 stocks. Takes 3–4 minutes.
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const loading        = ref(false)
const loadingDrill   = ref(false)
const loadingPeriod  = ref(false)
const progress       = ref('')
const error          = ref('')
const sectors        = ref([])
const selectedSector = ref('')
const drillResult    = ref(null)
const periodData     = ref([])

const API_BASE = 'http://localhost:60363'

// CHANGE 3: Navigate to Findings tab
function goToFindings() {
  window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'findings' }))
}

async function runAllSectors() {
  loading.value  = true
  error.value    = ''
  sectors.value  = []
  progress.value = 'fetching'
  try {
    const res  = await fetch(`${API_BASE}/research/all-sectors`)
    const data = await res.json()
    if (data.error) { error.value = data.error; return }
    sectors.value = data.sectors ?? []
  } catch (e) {
    error.value = `Could not run sector research: ${e.message}`
  } finally {
    loading.value = false
  }
}

async function runPeriodValidation() {
  loadingPeriod.value = true
  error.value         = ''
  periodData.value    = []
  const sectorNames   = sectors.value.map(s => s.sector)
  const results       = []
  try {
    for (const sector of sectorNames) {
      try {
        const res  = await fetch(`${API_BASE}/backtest/period/2006/2016/sector/${sector}`)
        const text = await res.text()
        results.push(parsePeriodReport(text, sector))
      } catch { /* skip failed sector */ }
    }
    periodData.value = results
  } finally {
    loadingPeriod.value = false
  }
}

function parsePeriodReport(text, sector) {
  const lines = text.split('\n')
  const beatLine = lines.find(l => l.includes('symbols beat buy-and-hold'))
  let beatCount = 0, symbolsTested = 0
  if (beatLine) {
    const m = beatLine.match(/(\d+) of (\d+)/)
    if (m) { beatCount = +m[1]; symbolsTested = +m[2] }
  }
  const advantages = []
  for (const line of lines) {
    const m = line.match(/Strategy:\s*([-\d,.]+)%.*Buy&Hold:\s*([-\d,.]+)%/)
    if (m) {
      const strat = parseFloat(m[1].replace(/\./g, '').replace(',', '.'))
      const bah   = parseFloat(m[2].replace(/\./g, '').replace(',', '.'))
      advantages.push(strat - bah)
    }
  }
  let medianAdvantage = 0
  if (advantages.length) {
    const sorted = [...advantages].sort((a, b) => a - b)
    const mid    = Math.floor(sorted.length / 2)
    medianAdvantage = sorted.length % 2 !== 0
      ? sorted[mid]
      : (sorted[mid - 1] + sorted[mid]) / 2
    medianAdvantage = Math.round(medianAdvantage * 10) / 10
  }
  return { sector, beatCount, symbolsTested, medianAdvantage }
}

async function drillDown() {
  if (!selectedSector.value) return
  loadingDrill.value = true
  drillResult.value  = null
  try {
    const res  = await fetch(`${API_BASE}/research/sector/${selectedSector.value}`)
    const data = await res.json()
    if (data.error) { error.value = data.error; return }
    drillResult.value = data
  } catch (e) {
    error.value = `Could not load sector: ${e.message}`
  } finally {
    loadingDrill.value = false
  }
}

const sectorsBeat = computed(() => sectors.value.filter(s => s.verdict === 'Outperformed').length)
const bestSector  = computed(() => [...sectors.value].sort((a, b) => b.medianAdvantage - a.medianAdvantage)[0] ?? null)
const worstSector = computed(() => [...sectors.value].sort((a, b) => a.medianAdvantage - b.medianAdvantage)[0] ?? null)
const totalStocks = computed(() => sectors.value.reduce((sum, s) => sum + (s.symbolsTested ?? 0), 0))
const totalBeat   = computed(() => sectors.value.reduce((sum, s) => sum + (s.beatCount ?? 0), 0))

const overallMedianAdvantage = computed(() => {
  const vals = sectors.value.map(s => s.medianAdvantage).filter(v => v != null)
  if (!vals.length) return 0
  const sorted = [...vals].sort((a, b) => a - b)
  const mid    = Math.floor(sorted.length / 2)
  return sorted.length % 2 !== 0
    ? sorted[mid]
    : Math.round((sorted[mid - 1] + sorted[mid]) * 100 / 2) / 100
})

const marketVerdict = computed(() => {
  const beatPct = totalBeat.value / Math.max(totalStocks.value, 1)
  if (overallMedianAdvantage.value >= 0 && beatPct >= 0.5)
    return { icon: '🟢', text: 'Outperformed Benchmark', cssClass: 'mv-positive' }
  if (overallMedianAdvantage.value >= 0 || beatPct >= 0.5)
    return { icon: '🟡', text: 'Mixed Results',          cssClass: 'mv-mixed'    }
  return { icon: '🔴', text: 'Underperformed Benchmark', cssClass: 'mv-negative' }
})

const winningSectors = computed(() => [...sectors.value].filter(s => s.medianAdvantage > 0).sort((a, b) => b.medianAdvantage - a.medianAdvantage))
const losingSectors  = computed(() => [...sectors.value].filter(s => s.medianAdvantage <= 0).sort((a, b) => a.medianAdvantage - b.medianAdvantage))

const periodComparison = computed(() => {
  if (!periodData.value.length) return []
  return sectors.value.map(s => {
    const p       = periodData.value.find(d => d.sector === s.sector)
    const beat1   = s.beatCount
    const tested1 = s.symbolsTested
    const median1 = s.medianAdvantage
    const beat2   = p?.beatCount       ?? 0
    const tested2 = p?.symbolsTested   ?? 0
    const median2 = p?.medianAdvantage ?? 0
    const beatPct1 = tested1 > 0 ? beat1 / tested1 : 0
    const beatPct2 = tested2 > 0 ? beat2 / tested2 : 0
    let heldUp = 'No'
    if (beatPct1 >= 0.5 && beatPct2 >= 0.5 && median1 > 0 && median2 > 0) heldUp = 'Yes'
    else if (beatPct2 >= 0.5 && median2 > 0) heldUp = 'Partial'
    return {
      sector: s.sector,
      beat2016_2026: beat1, tested2016_2026: tested1, median2016_2026: median1,
      beat2006_2016: beat2, tested2006_2016: tested2, median2006_2016: median2,
      heldUp
    }
  })
})

const periodFinding = computed(() => {
  if (!periodComparison.value.length) return ''
  const held    = periodComparison.value.filter(r => r.heldUp === 'Yes').map(r => r.sector)
  const partial = periodComparison.value.filter(r => r.heldUp === 'Partial').map(r => r.sector)
  if (held.length === 0)
    return `No sector maintained a consistent RSI edge across both periods. The 2016–2026 outperformers did not reliably survive the 2006–2016 regime. This suggests the earlier results were driven by market conditions specific to that decade, not a durable strategy edge.`
  const heldStr    = held.map(s => s.charAt(0).toUpperCase() + s.slice(1)).join(', ')
  const partialStr = partial.length ? ` ${partial.map(s => s.charAt(0).toUpperCase() + s.slice(1)).join(', ')} showed partial survival.` : ''
  return `${heldStr} ${held.length === 1 ? 'is the only sector' : 'are the only sectors'} that maintained a positive RSI edge across both decades — surviving both the 2008 financial crisis and the 2014–2016 oil price collapse.${partialStr} All other sectors failed to hold up. This is the strongest validation signal so far: the ${heldStr} pattern appears durable, not regime-specific.`
})

function confidenceScore(row) {
  const beatPct1 = row.tested2016_2026 > 0 ? row.beat2016_2026 / row.tested2016_2026 : 0
  const beatPct2 = row.tested2006_2016 > 0 ? row.beat2006_2016 / row.tested2006_2016 : 0
  const medScore1 = row.median2016_2026 > 0 ? 1 : 0
  const medScore2 = row.median2006_2016 > 0 ? 1 : 0
  return Math.round((beatPct1 * 30) + (beatPct2 * 30) + (medScore1 * 20) + (medScore2 * 20))
}

function scoreColor(score) {
  if (score >= 60) return 'score-high'
  if (score >= 35) return 'score-mid'
  return 'score-low'
}
</script>

<style scoped>
.sr-panel {
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

.sr-header         { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.sr-header-actions { display: flex; gap: 6px; }
.sr-title          { font-size: 14px; font-weight: 700; }
.sr-run-btn { padding: 8px 14px; border-radius: 7px; border: none; background: var(--color-accent); color: #fff; font-size: 12px; font-weight: 600; cursor: pointer; transition: opacity 0.15s; }
.sr-run-btn--secondary { background: var(--color-bg2); color: var(--color-text); border: 1px solid var(--color-border); }
.sr-run-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.sr-error { padding: 8px 10px; background: rgba(239,68,68,.12); color: var(--color-negative); border-radius: 6px; font-size: 12px; }

/* CHANGE 1: Research history banner */
.sr-history-banner {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-left: 3px solid #818cf8;
  border-radius: 0 7px 7px 0;
  color: #818cf8;
}

.sr-market-summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; }
.sr-ms-stat { background: var(--color-bg2); border-radius: 7px; padding: 8px 10px; text-align: center; }
.sr-ms-primary   { border: 1px solid var(--color-accent); }
.sr-ms-label     { font-size: 10px; color: var(--color-muted); margin-bottom: 2px; }
.sr-ms-value     { font-size: 15px; font-weight: 700; }
.sr-ms-highlight { color: var(--color-positive); }
.sr-ms-dim       { color: var(--color-negative); }

.sr-market-verdict { display: flex; gap: 16px; align-items: flex-start; padding: 12px 14px; border-radius: 9px; border: 1px solid transparent; flex-wrap: wrap; }
.mv-positive { background: rgba(34,197,94,.1);  border-color: rgba(34,197,94,.3); }
.mv-mixed    { background: rgba(234,179,8,.1);  border-color: rgba(234,179,8,.3); }
.mv-negative { background: rgba(239,68,68,.08); border-color: rgba(239,68,68,.25); }
.sr-mv-left     { min-width: 180px; }
.sr-mv-label    { font-size: 10px; font-weight: 700; letter-spacing: 0.09em; color: var(--color-muted); margin-bottom: 4px; }
.sr-mv-headline { font-size: 15px; font-weight: 700; margin-bottom: 2px; }
.sr-mv-sub      { font-size: 12px; color: var(--color-muted); }
.sr-mv-stats    { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; flex: 1; min-width: 280px; }
.sr-mv-stat       { text-align: center; }
.sr-mv-stat-label { font-size: 10px; color: var(--color-muted); margin-bottom: 2px; }
.sr-mv-stat-value { font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }

.srr-wrap { background: var(--color-bg2); border: 1px solid var(--color-border); border-radius: 10px; padding: 14px 16px; display: flex; flex-direction: column; gap: 12px; }
.srr-wrap--period { border-color: rgba(234,179,8,.35); background: rgba(234,179,8,.04); }
.srr-header { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.srr-title  { font-size: 13px; font-weight: 700; }
.srr-period { font-size: 11px; color: var(--color-muted); }
.srr-finding { background: var(--color-bg); border-radius: 7px; padding: 10px 12px; }
.srr-finding--period  { border-left: 3px solid var(--color-partial); }
.srr-finding-label    { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 5px; }
.srr-finding-text     { font-size: 12px; line-height: 1.6; }

.srr-cols { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.srr-col { background: var(--color-bg); border-radius: 7px; padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
.srr-col-label    { font-size: 10px; font-weight: 700; letter-spacing: 0.07em; color: var(--color-muted); margin-bottom: 2px; }
.srr-sector-row   { display: flex; align-items: center; gap: 6px; font-size: 12px; }
.srr-sector-name  { flex: 1; text-transform: capitalize; font-weight: 600; }
.srr-sector-beat  { color: var(--color-muted); font-size: 11px; }
.srr-sector-adv   { font-weight: 700; font-variant-numeric: tabular-nums; min-width: 52px; text-align: right; }
.srr-none         { font-size: 11px; color: var(--color-muted); font-style: italic; }

.srr-hypotheses { background: var(--color-bg); border-radius: 7px; padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
.srr-hyp-label   { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 2px; }
.srr-hyp-row     { display: flex; align-items: flex-start; gap: 8px; font-size: 12px; }
.srr-hyp-verdict { font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; white-space: nowrap; flex-shrink: 0; margin-top: 1px; }
.srr-hyp-rejected  { background: rgba(239,68,68,.12); color: var(--color-negative); }
.srr-hyp-supported { background: rgba(34,197,94,.12); color: var(--color-positive); }
/* CHANGE: Partial hypothesis style */
.srr-hyp-partial   { background: rgba(234,179,8,.12); color: var(--color-partial); }
.srr-hyp-text      { color: var(--color-muted); line-height: 1.5; }

.srr-caveats { font-size: 10px; color: var(--color-muted); line-height: 1.5; padding-top: 2px; }

.srr-period-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; min-width: 0; }
.srr-period-card { background: var(--color-bg); border-radius: 7px; padding: 8px 10px; border-left: 3px solid var(--color-border); display: flex; flex-direction: column; gap: 6px; min-width: 0; }
.card-beat    { border-left-color: var(--color-positive); }
.card-partial { border-left-color: var(--color-partial);  }
.card-miss    { border-left-color: var(--color-negative); }
.srr-pc-top   { display: flex; align-items: center; justify-content: space-between; gap: 4px; }
.srr-pc-sector       { font-size: 11px; font-weight: 700; text-transform: capitalize; }
.srr-pc-row          { display: flex; align-items: center; gap: 6px; }
.srr-pc-period       { flex: 1; text-align: center; }
.srr-pc-period-label { font-size: 9px; color: var(--color-muted); margin-bottom: 2px; }
.srr-pc-beat         { font-size: 11px; font-weight: 600; }
.srr-pc-adv          { font-size: 12px; font-weight: 700; font-variant-numeric: tabular-nums; }
.srr-pc-divider      { color: var(--color-muted); font-size: 12px; flex-shrink: 0; }

.sr-table-wrap, .sr-drill-table-wrap { overflow-x: auto; }
.sr-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.sr-table th { text-align: left; padding: 6px 8px; font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: var(--color-muted); border-bottom: 1px solid var(--color-border); white-space: nowrap; }
.sr-table td { padding: 8px; border-bottom: 1px solid var(--color-border); vertical-align: middle; }
.row-beat    { background: rgba(34,197,94,.04); }
.row-partial { background: rgba(234,179,8,.06); }
.row-miss    { background: transparent; }
.sr-table tbody tr:hover { background: var(--color-bg2); }
.td-sector   { font-weight: 600; text-transform: capitalize; }
.td-symbol   { font-weight: 700; font-family: monospace; font-size: 12px; }
.td-small    { font-size: 11px; }
.td-positive { color: var(--color-positive); font-weight: 600; }
.td-negative { color: var(--color-negative); }

.verdict-chip  { font-size: 10px; font-weight: 600; padding: 2px 7px; border-radius: 4px; white-space: nowrap; }
.chip-beat     { background: rgba(34,197,94,.15); color: var(--color-positive); }
.chip-partial  { background: rgba(234,179,8,.15); color: var(--color-partial);  }
.chip-miss     { background: rgba(239,68,68,.12); color: var(--color-negative); }

.sr-drilldown       { display: flex; flex-direction: column; gap: 8px; }
.sr-drilldown-label { font-size: 10px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--color-muted); }
.sr-drilldown-row   { display: flex; gap: 6px; }
.sr-select { flex: 1; padding: 7px 10px; border-radius: 7px; border: 1px solid var(--color-border); background: var(--color-bg2); color: var(--color-text); font-size: 12px; text-transform: capitalize; outline: none; }
.sr-select:focus { border-color: var(--color-accent); }
.sr-drill-btn { padding: 7px 12px; border-radius: 7px; border: 1px solid var(--color-border); background: var(--color-bg2); color: var(--color-text); font-size: 12px; cursor: pointer; }
.sr-drill-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.sr-drill-header { font-size: 12px; font-weight: 600; padding: 6px 0; }

/* CHANGE 2+3: Conclusion banner */
.sr-conclusion-banner {
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-accent);
  border-radius: 0 8px 8px 0;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.sr-conclusion-title  { font-size: 11px; font-weight: 700; letter-spacing: 0.06em; color: var(--color-muted); text-transform: uppercase; }
.sr-conclusion-text   { font-size: 12px; line-height: 1.6; color: var(--color-text); }
.sr-conclusion-action { font-size: 11px; color: var(--color-muted); }
.sr-findings-link {
  background: none;
  border: none;
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  padding: 0;
  text-decoration: underline;
}
.sr-findings-link:hover { opacity: 0.8; }

.srr-pc-score { display: flex; align-items: center; gap: 6px; margin-top: 4px; }
.srr-pc-score-bar-wrap { flex: 1; height: 4px; background: var(--color-border); border-radius: 2px; overflow: hidden; }
.srr-pc-score-bar { height: 100%; border-radius: 2px; transition: width 0.4s ease; }
.srr-pc-score-label { font-size: 10px; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
.srr-pc-score-bar.score-high { background: var(--color-positive); }
.srr-pc-score-bar.score-mid  { background: var(--color-partial);  }
.srr-pc-score-bar.score-low  { background: var(--color-negative); }
.srr-pc-score-label.score-high { color: var(--color-positive); }
.srr-pc-score-label.score-mid  { color: var(--color-partial);  }
.srr-pc-score-label.score-low  { color: var(--color-negative); }

.sr-empty { text-align: center; padding: 2.5rem 1rem; color: var(--color-muted); font-size: 13px; line-height: 1.7; }
</style>