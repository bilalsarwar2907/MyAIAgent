<template>
  <div class="pr-panel">

    <div class="pr-header">
      <span class="pr-title">Portfolio Research</span>
    </div>

    <!-- Ticker input -->
    <div class="pr-input-row">
      <input
        v-model="tickerInput"
        class="pr-input"
        placeholder="AAPL, XOM, MSFT, NVDA…"
        @keyup.enter="runResearch"
        @input="tickerInput = tickerInput.toUpperCase()"
      />
      <button class="pr-btn" :disabled="loading" @click="runResearch">
        {{ loading ? `${progress}…` : 'Research All' }}
      </button>
    </div>

    <div v-if="error" class="pr-error">{{ error }}</div>

    <!-- Results -->
    <div v-if="results.length" class="pr-results">

      <!-- CHANGE 1: Positioning banner — always shown with results -->
      <div class="pr-positioning-banner">
        🔬 <strong>Research and validation only.</strong>
        Trading decisions must come from the <strong>Screener + Rulebook</strong>.
        Do not modify strategy based on these results.
      </div>

      <!-- CHANGE 2: Finding #1 connection — shown when strong-trend stocks detected -->
      <div v-if="strongTrendStocks.length > 0" class="pr-finding-connection">
        📌 <strong>Supports Finding #1:</strong>
        {{ strongTrendStocks.join(', ') }} {{ strongTrendStocks.length === 1 ? 'has' : 'have' }}
        a B&amp;H return above 300% — RSI strategy is not validated on strong-trending stocks.
        This is why they are excluded by your Screener.
      </div>

      <!-- Summary bar -->
      <div class="pr-summary">
        <div class="pr-summary-stat">
          <div class="pr-stat-label">Stocks researched</div>
          <div class="pr-stat-value">{{ results.length }}</div>
        </div>
        <div class="pr-summary-stat pr-summary-primary">
          <div class="pr-stat-label">Strategy beats B&amp;H</div>
          <div class="pr-stat-value">{{ beatCount }}/{{ results.length }}</div>
        </div>
        <div class="pr-summary-stat">
          <div class="pr-stat-label">Median B&amp;H return</div>
          <div class="pr-stat-value">{{ medianBaH.toFixed(1) }}%</div>
        </div>
        <div class="pr-summary-stat">
          <div class="pr-stat-label">Median best strategy</div>
          <div class="pr-stat-value">{{ medianBest.toFixed(1) }}%</div>
        </div>
        <div class="pr-summary-stat" :class="medianAdvantage >= 0 ? 'pr-summary-positive' : 'pr-summary-negative'">
          <div class="pr-stat-label">Median advantage</div>
          <div class="pr-stat-value">{{ medianAdvantage >= 0 ? '+' : '' }}{{ medianAdvantage.toFixed(1) }}%</div>
        </div>
      </div>

      <!-- Comparison table -->
      <div class="pr-table-wrap">
        <table class="pr-table">
          <thead>
            <tr>
              <th>Stock</th>
              <th>Buy &amp; Hold</th>
              <th>Best Strategy</th>
              <th>Advantage</th>
              <th>Trades</th>
              <th>Win %</th>
              <th>Max DD</th>
              <th>Evidence</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in sortedResults" :key="r.symbol"
                :class="[r.bestStrategy && r.bestStrategy.beatBuyAndHold ? 'row-beat' : 'row-miss',
                         isStrongTrend(r) ? 'row-excluded' : '']">
              <td class="td-symbol">
                {{ r.symbol }}
                <!-- CHANGE 2: inline excluded tag for strong-trend stocks -->
                <span v-if="isStrongTrend(r)" class="td-excluded-tag">🚫 Excl.</span>
              </td>
              <td :class="isStrongTrend(r) ? 'td-strong-trend' : ''">
                {{ r.bahReturn.toFixed(1) }}%
              </td>
              <td class="td-strategy">{{ r.bestStrategy ? r.bestStrategy.name : '—' }}</td>
              <td :class="r.advantage >= 0 ? 'td-positive' : 'td-negative'">
                {{ r.advantage >= 0 ? '+' : '' }}{{ r.advantage.toFixed(1) }}%
              </td>
              <td>{{ r.bestStrategy ? r.bestStrategy.trades : '—' }}</td>
              <td>{{ r.bestStrategy ? r.bestStrategy.winRate + '%' : '—' }}</td>
              <td class="td-negative">
                {{ r.bestStrategy && r.bestStrategy.maxDrawdown < 0
                    ? r.bestStrategy.maxDrawdown.toFixed(1) + '%'
                    : '—' }}
              </td>
              <td>
                <span class="evidence-badge"
                  :class="evidenceClass(r.bestStrategy, r.advantage, r.tradingDays)">
                  {{ evidenceLabel(r.bestStrategy, r.advantage, r.tradingDays) }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Portfolio-level insights narrative -->
      <div class="pr-insights" v-if="results.length >= 2">
        <div class="pr-insights-label">PORTFOLIO FINDINGS</div>

        <div class="pr-verdict" :class="portfolioVerdict.cssClass">
          <span class="pr-verdict-icon">{{ portfolioVerdict.icon }}</span>
          <div>
            <div class="pr-verdict-label">PORTFOLIO VERDICT</div>
            <div class="pr-verdict-text">{{ portfolioVerdict.text }}</div>
            <div class="pr-verdict-sub">Median advantage: {{ medianAdvantage >= 0 ? '+' : '' }}{{ medianAdvantage.toFixed(1) }}%</div>
          </div>
        </div>

        <ul class="pr-insights-list">
          <li>{{ beatCount }} of {{ results.length }} stocks had a strategy that beat buy-and-hold</li>
          <li v-if="bestPerformer">Best result: <strong>{{ bestPerformer.symbol }}</strong>
            (+{{ bestPerformer.advantage.toFixed(1) }}% advantage)</li>
          <li v-if="worstPerformer">Worst result: <strong>{{ worstPerformer.symbol }}</strong>
            ({{ worstPerformer.advantage.toFixed(1) }}% vs benchmark)</li>
        </ul>

        <div class="pr-strategy-wins" v-if="strategyWins.length">
          <div class="pr-strategy-wins-label">Most successful strategy per stock</div>
          <div class="pr-strategy-wins-list">
            <div v-for="sw in strategyWins" :key="sw.name" class="pr-strategy-win-row">
              <span class="pr-sw-name">{{ sw.name }}</span>
              <span class="pr-sw-count">{{ sw.count }} stock{{ sw.count !== 1 ? 's' : '' }}</span>
              <span class="pr-sw-median" :class="sw.medianAdvantage >= 0 ? 'td-positive' : 'td-negative'">
                {{ sw.medianAdvantage >= 0 ? '+' : '' }}{{ sw.medianAdvantage.toFixed(1) }}% median
              </span>
            </div>
          </div>
        </div>

        <div class="pr-conclusion">
          <span class="pr-conclusion-label">Conclusion</span>
          <span v-if="beatCount === 0">
            No strategy outperformed buy-and-hold on any stock in this portfolio.
            Buy-and-hold was the stronger approach for every stock in this set,
            with a median advantage of {{ Math.abs(medianAdvantage).toFixed(1) }}% in favour of the benchmark.
          </span>
          <span v-else-if="beatCount === results.length">
            Every stock in this portfolio had at least one strategy that outperformed buy-and-hold.
            The median advantage was +{{ medianAdvantage.toFixed(1) }}%,
            suggesting these stocks exhibited price behaviour that RSI-based strategies can exploit.
          </span>
          <span v-else>
            RSI-based strategies outperformed buy-and-hold on {{ beatCount }} of {{ results.length }} stocks,
            but the overall median advantage was {{ medianAdvantage >= 0 ? '+' : '' }}{{ medianAdvantage.toFixed(1) }}%.
            The strongest results appeared on {{ winnersLabel }},
            while the strategies substantially underperformed on {{ losersLabel }},
            which {{ losers.length > 1 ? 'appear' : 'appears' }} to exhibit strong trending behaviour
            where buy-and-hold is historically difficult to beat.
          </span>
        </div>
      </div>

    </div>

    <div v-if="!results.length && !loading && !error" class="pr-empty">
      Enter two or more tickers separated by commas to compare how strategies performed across your portfolio.
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const tickerInput = ref('')
const loading     = ref(false)
const progress    = ref('')
const error       = ref('')
const results     = ref([])

const API_BASE = 'http://localhost:60363'

async function runResearch() {
  const symbols = tickerInput.value
    .split(',')
    .map(s => s.trim().toUpperCase())
    .filter(s => s.length > 0 && s.length <= 5)

  if (symbols.length === 0) return

  loading.value = true
  error.value   = ''
  results.value = []

  const fetched = []

  for (let i = 0; i < symbols.length; i++) {
    const sym = symbols[i]
    progress.value = `${sym} (${i + 1}/${symbols.length})`

    try {
      const res  = await fetch(`${API_BASE}/research/${sym}`)
      const text = await res.text()
      const parsed = parseReport(sym, text)
      if (parsed) fetched.push(parsed)
    } catch {
      // Skip failed symbols silently
    }

    if (i < symbols.length - 1) await new Promise(r => setTimeout(r, 400))
  }

  results.value = fetched
  loading.value = false
}

function parseReport(symbol, text) {
  const lines = text.split('\n').map(l => l.trim()).filter(Boolean)
  const periodLine  = lines.find(l => l.startsWith('Period:')) || ''
  const periodMatch = periodLine.match(/(\d{4}-\d{2}-\d{2}).*?(\d{4}-\d{2}-\d{2}).*?(\d+) trading/)

  const strategies = []
  let current = null

  for (const line of lines) {
    if (line.startsWith('📊')) {
      if (current) strategies.push(current)
      current = { name: line.replace('📊','').trim(), isBaseline: false,
                  returnPct: 0, trades: 0, winRate: 0, maxDrawdown: 0,
                  beatBuyAndHold: false }
    } else if (current) {
      if (line.startsWith('Return:'))  current.returnPct = parseFloat(line.replace('Return:','').replace('%','').replace(',','.').trim())
      if (line.startsWith('Verdict:')) {
        current.isBaseline     = line.includes('Baseline')
        current.beatBuyAndHold = line.toLowerCase().includes('beat')
      }
      const tm = line.match(/Trades:\s*(\d+)/)
      if (tm) current.trades = parseInt(tm[1])
      const wm = line.match(/Win rate:\s*([\d.,]+)%/)
      if (wm) current.winRate = parseFloat(wm[1].replace(',','.'))
      const dm = line.match(/Max drawdown:\s*(-?[\d.,]+)%/)
      if (dm) current.maxDrawdown = parseFloat(dm[1].replace(',','.'))
    }
  }
  if (current) strategies.push(current)

  const baseline     = strategies.find(s => s.isBaseline)
  const nonBaseline  = strategies.filter(s => !s.isBaseline)
  const bestStrategy = nonBaseline.sort((a, b) => b.returnPct - a.returnPct)[0] ?? null

  return {
    symbol,
    bahReturn:   baseline?.returnPct ?? 0,
    bestStrategy,
    advantage:   bestStrategy ? Math.round((bestStrategy.returnPct - (baseline?.returnPct ?? 0)) * 100) / 100 : 0,
    tradingDays: parseInt(periodMatch?.[3] ?? '0')
  }
}

// CHANGE 2: Detect strong-trend stocks (B&H > 300% = excluded by Finding #1)
function isStrongTrend(r) {
  return r.bahReturn > 300
}

const strongTrendStocks = computed(() =>
  results.value.filter(r => isStrongTrend(r)).map(r => r.symbol)
)

function evidenceClass(strategy, advantage, tradingDays) {
  if (!strategy) return 'ev-weak'
  const years = Math.round(tradingDays / 252)
  if (strategy.trades >= 50 && years >= 5 && advantage >= 15) return 'ev-strong'
  if (strategy.trades >= 15 && years >= 5 && advantage > 0)   return 'ev-moderate'
  return 'ev-weak'
}

function evidenceLabel(strategy, advantage, tradingDays) {
  const cls = evidenceClass(strategy, advantage, tradingDays)
  return cls === 'ev-strong' ? '🟢 Strong' : cls === 'ev-moderate' ? '🟡 Moderate' : '🔴 Weak'
}

const sortedResults = computed(() =>
  [...results.value].sort((a, b) => b.advantage - a.advantage)
)

const bestPerformer  = computed(() => sortedResults.value.filter(r => r.bestStrategy)[0] ?? null)
const worstPerformer = computed(() => {
  const valid = sortedResults.value.filter(r => r.bestStrategy)
  return valid[valid.length - 1] ?? null
})

const medianAdvantage = computed(() => median(results.value.map(r => r.advantage)))
const winnersLabel    = computed(() => results.value.filter(r => r.bestStrategy?.beatBuyAndHold).map(r => r.symbol).join(', ') || '—')
const losers          = computed(() => results.value.filter(r => !r.bestStrategy?.beatBuyAndHold))
const losersLabel     = computed(() => losers.value.map(r => r.symbol).join(', ') || '—')

const portfolioVerdict = computed(() => {
  const majorityFailed = beatCount.value < results.value.length / 2
  const medNeg = medianAdvantage.value < 0
  if (medNeg || majorityFailed) return { icon: '🔴', text: 'Underperformed Benchmark', cssClass: 'verdict-negative' }
  return { icon: '🟢', text: 'Outperformed Benchmark', cssClass: 'verdict-positive' }
})

const strategyWins = computed(() => {
  const groups = {}
  for (const r of results.value) {
    if (!r.bestStrategy) continue
    const name = r.bestStrategy.name
    if (!groups[name]) groups[name] = { count: 0, advantages: [] }
    groups[name].count++
    groups[name].advantages.push(r.advantage)
  }
  return Object.entries(groups)
    .map(([name, g]) => ({ name, count: g.count, medianAdvantage: median(g.advantages) }))
    .sort((a, b) => b.medianAdvantage - a.medianAdvantage)
})

const beatCount = computed(() => results.value.filter(r => r.bestStrategy?.beatBuyAndHold).length)
const medianBaH  = computed(() => median(results.value.map(r => r.bahReturn)))
const medianBest = computed(() => median(results.value.filter(r => r.bestStrategy).map(r => r.bestStrategy.returnPct)))

function median(arr) {
  if (!arr.length) return 0
  const sorted = [...arr].sort((a, b) => a - b)
  const mid = Math.floor(sorted.length / 2)
  return sorted.length % 2 !== 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2
}
</script>

<style scoped>
.pr-panel {
  --color-bg:       var(--bg-panel);
  --color-bg2:      var(--bg-panel-item);
  --color-border:   var(--bg-panel-border);
  --color-text:     var(--text-primary);
  --color-muted:    var(--text-secondary);
  --color-accent:   var(--accent);
  --color-positive: #22c55e;
  --color-negative: #ef4444;

  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 1rem;
  min-width: 560px;
  background: var(--color-bg);
  border-radius: 10px;
  color: var(--color-text);
}

.pr-header { display: flex; align-items: center; gap: 8px; }
.pr-title  { font-size: 14px; font-weight: 700; }

.pr-input-row { display: flex; gap: 6px; }
.pr-input {
  flex: 1; padding: 8px 10px; border-radius: 7px;
  border: 1px solid var(--color-border); background: var(--color-bg2);
  color: var(--color-text); font-size: 13px; font-family: monospace;
  text-transform: uppercase; letter-spacing: 0.04em;
}
.pr-input::placeholder { text-transform: none; letter-spacing: 0; opacity: 0.5; }
.pr-input:focus { outline: 2px solid var(--color-accent); outline-offset: 1px; }
.pr-btn {
  padding: 8px 14px; border-radius: 7px; border: none;
  background: var(--color-accent); color: #fff; font-size: 12px;
  font-weight: 600; cursor: pointer; white-space: nowrap;
}
.pr-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.pr-error { padding: 8px 10px; background: rgba(239,68,68,.12); color: var(--color-negative); border-radius: 6px; font-size: 12px; }

/* CHANGE 1: Positioning banner */
.pr-positioning-banner {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(234,179,8,.08);
  border: 1px solid rgba(234,179,8,.25);
  border-left: 3px solid #eab308;
  border-radius: 0 7px 7px 0;
  color: #eab308;
}

/* CHANGE 2: Finding #1 connection banner */
.pr-finding-connection {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.25);
  border-left: 3px solid #818cf8;
  border-radius: 0 7px 7px 0;
  color: #818cf8;
}

/* Summary bar */
.pr-summary { display: grid; grid-template-columns: repeat(5, 1fr); gap: 6px; margin-bottom: 4px; }
.pr-summary-stat { background: var(--color-bg2); border-radius: 7px; padding: 8px 10px; text-align: center; }
.pr-stat-label { font-size: 10px; color: var(--color-muted); margin-bottom: 2px; }
.pr-stat-value { font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.pr-summary-primary  { border: 1px solid var(--color-accent); }
.pr-summary-positive .pr-stat-value { color: var(--color-positive); }
.pr-summary-negative .pr-stat-value { color: var(--color-negative); }

/* Table */
.pr-table-wrap { overflow-x: auto; }
.pr-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.pr-table th {
  text-align: left; padding: 6px 8px; font-size: 10px; font-weight: 700;
  text-transform: uppercase; letter-spacing: 0.06em; color: var(--color-muted);
  border-bottom: 1px solid var(--color-border); white-space: nowrap;
}
.pr-table td { padding: 8px 8px; border-bottom: 1px solid rgba(255,255,255,.04); font-variant-numeric: tabular-nums; }
.row-beat     { background: rgba(34,197,94,.04); }
.row-miss     { background: transparent; }
/* CHANGE 2: excluded rows dimmed */
.row-excluded { opacity: 0.6; }
.pr-table tbody tr:hover { background: var(--color-bg2); }

.td-symbol       { font-weight: 700; font-family: monospace; font-size: 13px; }
.td-strategy     { font-size: 11px; color: var(--color-muted); max-width: 120px; }
.td-positive     { color: var(--color-positive); font-weight: 600; }
.td-negative     { color: var(--color-negative); }
.td-strong-trend { color: var(--color-negative); font-weight: 700; }
/* CHANGE 2: excluded tag inline in symbol cell */
.td-excluded-tag {
  display: inline-block; margin-left: 5px; font-size: 9px; font-weight: 700;
  background: rgba(239,68,68,.12); color: #ef4444;
  border: 1px solid rgba(239,68,68,.25); border-radius: 3px; padding: 1px 4px;
  font-family: sans-serif;
}

/* Evidence badges */
.evidence-badge  { font-size: 10px; font-weight: 600; padding: 2px 6px; border-radius: 4px; white-space: nowrap; }
.ev-strong   { background: rgba(34,197,94,.15);  color: var(--color-positive); }
.ev-moderate { background: rgba(234,179,8,.15);  color: #eab308; }
.ev-weak     { background: rgba(239,68,68,.12);  color: var(--color-negative); }

/* Portfolio insights */
.pr-insights { background: var(--color-bg2); border-radius: 8px; padding: 12px 14px; border-left: 3px solid var(--color-accent); margin-top: 4px; }
.pr-insights-label { font-size: 10px; font-weight: 700; letter-spacing: 0.08em; color: var(--color-muted); margin-bottom: 8px; }
.pr-insights-list  { margin: 0 0 10px 0; padding-left: 16px; display: flex; flex-direction: column; gap: 4px; }
.pr-insights-list li { font-size: 12px; line-height: 1.5; }
.pr-conclusion { font-size: 12px; line-height: 1.6; padding-top: 8px; border-top: 1px solid var(--color-border); display: flex; gap: 6px; }
.pr-conclusion-label { font-weight: 700; color: var(--color-muted); white-space: nowrap; padding-top: 1px; }

.pr-verdict { display: flex; align-items: flex-start; gap: 10px; padding: 10px 12px; border-radius: 7px; margin-bottom: 10px; }
.verdict-positive { background: rgba(34,197,94,.12); border: 1px solid rgba(34,197,94,.3); }
.verdict-negative { background: rgba(239,68,68,.1);  border: 1px solid rgba(239,68,68,.25); }
.pr-verdict-icon  { font-size: 18px; padding-top: 1px; }
.pr-verdict-label { font-size: 10px; font-weight: 700; letter-spacing: 0.08em; color: var(--color-muted); }
.pr-verdict-text  { font-size: 14px; font-weight: 700; margin: 1px 0; }
.pr-verdict-sub   { font-size: 11px; color: var(--color-muted); }

.pr-strategy-wins       { margin: 8px 0; }
.pr-strategy-wins-label { font-size: 10px; font-weight: 700; letter-spacing: 0.07em; color: var(--color-muted); margin-bottom: 6px; text-transform: uppercase; }
.pr-strategy-wins-list  { display: flex; flex-direction: column; gap: 5px; }
.pr-strategy-win-row    { display: flex; align-items: center; gap: 8px; }
.pr-sw-name   { font-size: 11px; color: var(--color-text); width: 140px; flex-shrink: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.pr-sw-count  { font-size: 11px; color: var(--color-muted); width: 44px; flex-shrink: 0; text-align: right; }
.pr-sw-median { font-size: 11px; font-weight: 600; font-variant-numeric: tabular-nums; }

.pr-empty { text-align: center; padding: 2rem 0.5rem; color: var(--color-muted); font-size: 13px; line-height: 1.6; }
</style>