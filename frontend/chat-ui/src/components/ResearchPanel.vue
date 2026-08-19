<template>
  <div class="research-panel">

    <!-- Search -->
    <div class="research-search">
      <input
        v-model="symbolInput"
        class="search-input"
        placeholder="Ticker — AAPL, XOM, NVDA…"
        maxlength="10"
        @keyup.enter="runResearch"
        @input="symbolInput = symbolInput.toUpperCase()"
      />
      <button class="search-btn" :disabled="loading" @click="runResearch">
        {{ loading ? 'Researching…' : 'Research' }}
      </button>
    </div>

    <div v-if="error" class="research-error">{{ error }}</div>

    <div v-if="report">

      <!-- FIX 1: Frozen rulebook warning — always visible when results are shown -->
      <div class="rulebook-warning">
        ⚠️ <strong>Research mode</strong> — do not modify the Rulebook during validation.
        Use the Screener for all trading decisions.
      </div>

      <!-- Header -->
      <div class="research-header">
        <div class="research-ticker">{{ report.symbol }}</div>
        <div class="research-period">
          {{ report.periodStart }} → {{ report.periodEnd }}
          <span class="period-days">{{ report.tradingDays }} trading days</span>
        </div>
      </div>

      <!-- Key Finding box -->
      <div class="key-finding" :class="topStrategy && topStrategy.beatBuyAndHold ? 'finding-positive' : 'finding-negative'">
        <div class="finding-label">KEY FINDING</div>
        <div v-if="topStrategy && !topStrategy.isBaseline">
          <div class="finding-headline">
            For {{ report.symbol }},
            <span v-if="topStrategy.beatBuyAndHold">
              <strong>{{ topStrategy.name }}</strong> outperformed buy-and-hold.
            </span>
            <span v-else>
              no strategy outperformed buy-and-hold.
            </span>
          </div>
          <div class="finding-stats" v-if="topStrategy.beatBuyAndHold">
            Best strategy: <strong>{{ topStrategy.name }}</strong>
            &nbsp;·&nbsp;
            Advantage: <strong>+{{ gap(topStrategy).toFixed(2) }}% over benchmark</strong>
          </div>
          <div class="finding-stats" v-else>
            Best result: <strong>{{ topStrategy.name }}</strong> at
            <strong>{{ topStrategy.returnPct.toFixed(2) }}%</strong>
            vs buy-and-hold <strong>{{ bahReturn.toFixed(2) }}%</strong>
          </div>
        </div>
      </div>

      <!-- Confidence indicator -->
      <div class="confidence-box" :class="confidence.cssClass">
        <div class="confidence-left">
          <div class="confidence-label">EVIDENCE QUALITY</div>
          <div class="confidence-value">{{ confidence.icon }} {{ confidence.label }}</div>
        </div>
        <div class="confidence-reasons">
          <div v-for="r in confidence.reasons" :key="r" class="confidence-reason">{{ r }}</div>
        </div>
      </div>

      <!-- FIX 2: Strategy ranking with rulebook labels -->
      <div class="section-label">Strategy ranking</div>
      <div class="ranking-cards">
        <div
          v-for="(s, i) in sortedStrategies"
          :key="s.name"
          class="strategy-card"
          :class="{
            'card-winner':   i === 0 && !s.isBaseline,
            'card-baseline': s.isBaseline,
            'card-current':  isCurrentRulebook(s),
            'card-research': isResearchOnly(s)
          }"
        >
          <div class="card-rank">#{{ i + 1 }}</div>
          <div class="card-body">
            <div class="card-top-row">
              <div class="card-name-group">
                <div class="card-name">{{ s.name }}</div>
                <!-- FIX 2: Rulebook label -->
                <div v-if="isCurrentRulebook(s)" class="rulebook-label rulebook-label--current">
                  ✅ Current Rulebook Strategy
                </div>
                <div v-else-if="isResearchOnly(s)" class="rulebook-label rulebook-label--research">
                  🔬 Alternative (Research Only)
                </div>
              </div>
              <!-- Verdict badge -->
              <div v-if="!s.isBaseline" class="verdict-badge"
                :class="s.beatBuyAndHold ? 'badge-beat' : 'badge-miss'">
                {{ s.beatBuyAndHold ? '🟢 Beat benchmark' : '🔴 Missed benchmark' }}
              </div>
              <div v-else class="verdict-badge badge-baseline">📊 Baseline</div>
            </div>

            <!-- Metrics -->
            <div class="card-metrics">
              <div class="metric">
                <div class="metric-label">Return</div>
                <div class="metric-value" :class="s.returnPct >= 0 ? 'positive' : 'negative'">
                  {{ s.returnPct >= 0 ? '+' : '' }}{{ s.returnPct.toFixed(2) }}%
                </div>
              </div>
              <div class="metric" v-if="!s.isBaseline">
                <div class="metric-label">vs Buy &amp; Hold</div>
                <div class="metric-value" :class="gap(s) >= 0 ? 'positive' : 'negative'">
                  {{ gap(s) >= 0 ? '+' : '' }}{{ gap(s).toFixed(2) }}%
                </div>
              </div>
              <div class="metric" v-if="!s.isBaseline && s.trades > 0">
                <div class="metric-label">Win rate</div>
                <div class="metric-value">{{ s.winRate }}%</div>
              </div>
              <div class="metric" v-if="!s.isBaseline && s.trades > 0">
                <div class="metric-label">Trades</div>
                <div class="metric-value">{{ s.trades }}</div>
              </div>
              <div class="metric" v-if="!s.isBaseline && s.maxDrawdown < 0">
                <div class="metric-label">Max drawdown</div>
                <div class="metric-value negative">{{ s.maxDrawdown.toFixed(2) }}%</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Bar chart -->
      <div class="section-label" style="margin-top:1rem;">Return comparison</div>
      <div class="bar-chart">
        <div v-for="s in sortedStrategies" :key="s.name + '-bar'" class="bar-row">
          <div class="bar-name">{{ s.name }}</div>
          <div class="bar-track">
            <div
              class="bar-fill"
              :class="{
                'bar-baseline': s.isBaseline,
                'bar-current':  isCurrentRulebook(s),
                'bar-beat':     !s.isBaseline && !isCurrentRulebook(s) && s.beatBuyAndHold,
                'bar-miss':     !s.isBaseline && !isCurrentRulebook(s) && !s.beatBuyAndHold
              }"
              :style="{ width: barWidth(s.returnPct) }"
            ></div>
            <span class="bar-value">{{ s.returnPct.toFixed(1) }}%</span>
          </div>
        </div>
      </div>

      <!-- AI explanation -->
      <div class="ai-section">
        <div class="section-label">AI interpretation</div>
        <div v-if="loadingAI" class="ai-loading">Interpreting results…</div>
        <div v-else-if="aiExplanation" class="ai-text">{{ sanitised(aiExplanation) }}</div>
        <button v-if="!aiExplanation && !loadingAI" class="ai-btn" @click="fetchAIExplanation">
          Explain these results
        </button>
      </div>

      <!-- FIX 3: Action statement — always shown at bottom -->
      <div class="action-statement">
        📋 Use this tab to understand the strategy — not to change it.
        All trading decisions must follow the <strong>Screener + Rulebook</strong>.
        Do not add filters or modify thresholds based on this research.
      </div>

    </div>

    <div v-if="!report && !loading && !error" class="empty-state">
      Enter a ticker above to see how different strategies performed historically.
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const symbolInput   = ref('')
const loading       = ref(false)
const loadingAI     = ref(false)
const error         = ref('')
const report        = ref(null)
const aiExplanation = ref('')

const API_BASE = 'http://localhost:60363'

async function runResearch() {
  const sym = symbolInput.value.trim().toUpperCase()
  if (!sym) return

  loading.value       = true
  error.value         = ''
  report.value        = null
  aiExplanation.value = ''

  try {
    const res  = await fetch(`${API_BASE}/research/${sym}`)
    const text = await res.text()
    report.value = parseReport(sym, text)
  } catch (e) {
    error.value = `Could not fetch research for ${sym}: ${e.message}`
  } finally {
    loading.value = false
  }
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
                  beatBuyAndHold: false, verdict: '' }
    } else if (current) {
      if (line.startsWith('Rule:'))    current.rule    = line.replace('Rule:','').trim()
      if (line.startsWith('Return:'))  current.returnPct = parseFloat(line.replace('Return:','').replace('%','').replace(',','.').trim())
      if (line.startsWith('Verdict:')) {
        current.verdict        = line.replace('Verdict:','').trim()
        current.isBaseline     = current.verdict === 'Baseline'
        current.beatBuyAndHold = current.verdict.toLowerCase().includes('beat')
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

  return {
    symbol,
    periodStart: periodMatch?.[1] ?? '',
    periodEnd:   periodMatch?.[2] ?? '',
    tradingDays: periodMatch?.[3] ?? '',
    strategies
  }
}

const sortedStrategies = computed(() => {
  if (!report.value) return []
  return [...report.value.strategies].sort((a, b) => b.returnPct - a.returnPct)
})

const bahReturn = computed(() => {
  const b = report.value?.strategies.find(s => s.isBaseline)
  return b?.returnPct ?? 0
})

const topStrategy = computed(() =>
  sortedStrategies.value.find(s => !s.isBaseline) ?? null
)

// FIX 2: Identify which strategy matches the frozen rulebook
// Rulebook uses RSI(30/70) — plain RSI without MA filter
// Strategy names come from the backend text report parser
function isCurrentRulebook(s) {
  if (s.isBaseline) return false
  const name = (s.name ?? '').toLowerCase()
  // Matches "RSI(30/70)" but NOT "RSI(30/70) + 200-day MA filter"
  return name.includes('rsi') && !name.includes('ma') && !name.includes('filter') && !name.includes('200')
}

function isResearchOnly(s) {
  if (s.isBaseline) return false
  return !isCurrentRulebook(s)
}

const confidence = computed(() => {
  if (!report.value || !topStrategy.value) return { label: 'Unknown', icon: '⚪', cssClass: 'conf-weak', reasons: [] }

  const best      = topStrategy.value
  const trades    = best.trades
  const days      = parseInt(report.value.tradingDays) || 0
  const years     = Math.round(days / 252)
  const advantage = gap(best)

  const reasons = [
    `${trades} trades over ${years} year${years !== 1 ? 's' : ''}`,
    advantage > 0 ? `+${advantage.toFixed(1)}% advantage over benchmark` : `${advantage.toFixed(1)}% vs benchmark`
  ]

  if (trades >= 50 && years >= 5 && advantage >= 15) {
    return { label: 'Strong',   icon: '🟢', cssClass: 'conf-strong',   reasons }
  }
  if (trades >= 15 && years >= 5 && advantage > 0) {
    return { label: 'Moderate', icon: '🟡', cssClass: 'conf-moderate', reasons }
  }
  return { label: 'Weak', icon: '🔴', cssClass: 'conf-weak', reasons }
})

function gap(s) {
  return Math.round((s.returnPct - bahReturn.value) * 100) / 100
}

const maxAbsReturn = computed(() =>
  Math.max(...(report.value?.strategies.map(s => Math.abs(s.returnPct)) ?? [1]), 1)
)

function barWidth(returnPct) {
  return `${Math.max(2, (Math.abs(returnPct) / maxAbsReturn.value) * 100)}%`
}

// FIX 4: Sanitise AI output — remove unfilled placeholders like "X%", "Y trades", "Z years"
// The AI sometimes returns template-style text with unreplaced variables.
function sanitised(text) {
  if (!text) return ''
  return text
    // Remove standalone placeholder patterns: "X%", "Y trades", "Z years", etc.
    .replace(/\b[A-Z]\s*%/g, '[value]%')
    .replace(/\b[A-Z]\s+trades\b/g, 'some trades')
    .replace(/\b[A-Z]\s+years\b/g, 'several years')
    // Clean up any double spaces left behind
    .replace(/  +/g, ' ')
    .trim()
}

async function fetchAIExplanation() {
  if (!report.value) return
  loadingAI.value = true
  try {
    const res  = await fetch(`${API_BASE}/research/${report.value.symbol}/explain`)
    const data = await res.json()
    if (data.error) {
      aiExplanation.value = `Could not explain results: ${data.error}`
    } else {
      aiExplanation.value = data.explanation ?? 'No explanation returned.'
    }
  } catch (e) {
    aiExplanation.value = 'Could not fetch AI explanation.'
  } finally {
    loadingAI.value = false
  }
}
</script>

<style scoped>
.research-panel {
  --color-bg:        var(--bg-panel);
  --color-bg2:       var(--bg-panel-item);
  --color-border:    var(--bg-panel-border);
  --color-text:      var(--text-primary);
  --color-muted:     var(--text-secondary);
  --color-accent:    var(--accent);
  --color-positive:  #22c55e;
  --color-negative:  #ef4444;
}

.research-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
  width: 300px;
  min-width: 280px;
  background: var(--color-bg);
  border-radius: 10px;
  color: var(--color-text);
}

/* Search */
.research-search { display: flex; gap: 6px; }
.search-input {
  flex: 1;
  padding: 8px 10px;
  border-radius: 7px;
  border: 1px solid var(--color-border);
  background: var(--color-bg2);
  color: var(--color-text);
  font-size: 13px;
  font-family: monospace;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}
.search-input::placeholder { text-transform: none; letter-spacing: 0; opacity: 0.5; }
.search-input:focus { outline: 2px solid var(--color-accent); outline-offset: 1px; }
.search-btn {
  padding: 8px 12px;
  border-radius: 7px;
  border: none;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}
.search-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.research-error {
  padding: 8px 10px;
  background: rgba(239,68,68,.15);
  color: var(--color-negative);
  border-radius: 6px;
  font-size: 12px;
}

/* FIX 1: Frozen rulebook warning */
.rulebook-warning {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(234,179,8,.1);
  border: 1px solid rgba(234,179,8,.3);
  border-left: 3px solid #eab308;
  border-radius: 0 7px 7px 0;
  color: #eab308;
}

/* Header */
.research-header { display: flex; flex-direction: column; gap: 2px; }
.research-ticker { font-size: 22px; font-weight: 700; font-family: monospace; }
.research-period { font-size: 11px; color: var(--color-muted); }
.period-days     { margin-left: 4px; }

/* Key finding */
.key-finding {
  border-radius: 8px;
  padding: 10px 12px;
  border-left: 3px solid transparent;
}
.finding-positive { background: rgba(34,197,94,.1);  border-color: var(--color-positive); }
.finding-negative { background: rgba(239,68,68,.08); border-color: var(--color-negative); }
.finding-label    { font-size: 10px; font-weight: 700; letter-spacing: 0.1em; color: var(--color-muted); margin-bottom: 4px; }
.finding-headline { font-size: 12px; line-height: 1.5; margin-bottom: 4px; }
.finding-stats    { font-size: 11px; color: var(--color-muted); }

/* Section label */
.section-label {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-muted);
  margin-bottom: 6px;
}

/* Strategy cards */
.ranking-cards { display: flex; flex-direction: column; gap: 6px; }
.strategy-card {
  display: flex;
  gap: 8px;
  padding: 10px;
  border-radius: 8px;
  background: var(--color-bg2);
  border: 1px solid transparent;
}
.card-winner   { border-color: var(--color-accent); }
.card-baseline { opacity: 0.75; }
/* FIX 2: Current rulebook strategy gets accent border, research-only is subtle */
.card-current  { border-color: var(--color-positive) !important; background: rgba(34,197,94,.04); }
.card-research { opacity: 0.85; }

.card-rank { font-size: 10px; font-weight: 700; color: var(--color-muted); min-width: 18px; padding-top: 2px; }
.card-current .card-rank { color: var(--color-positive); }

.card-body    { flex: 1; min-width: 0; }
.card-top-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 6px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}
.card-name-group { display: flex; flex-direction: column; gap: 3px; }
.card-name       { font-size: 12px; font-weight: 600; }

/* FIX 2: Rulebook label pills */
.rulebook-label { font-size: 9px; font-weight: 700; padding: 2px 6px; border-radius: 3px; width: fit-content; }
.rulebook-label--current  { background: rgba(34,197,94,.15); color: #22c55e; }
.rulebook-label--research { background: rgba(148,163,184,.1); color: var(--color-muted); }

/* Verdict badge */
.verdict-badge  { font-size: 10px; font-weight: 600; padding: 2px 6px; border-radius: 4px; white-space: nowrap; }
.badge-beat     { background: rgba(34,197,94,.15);  color: var(--color-positive); }
.badge-miss     { background: rgba(239,68,68,.12);  color: var(--color-negative); }
.badge-baseline { background: rgba(255,255,255,.08); color: var(--color-muted); }

/* Metrics grid */
.card-metrics { display: grid; grid-template-columns: repeat(auto-fill, minmax(70px, 1fr)); gap: 6px; }
.metric         { display: flex; flex-direction: column; gap: 1px; }
.metric-label   { font-size: 10px; color: var(--color-muted); }
.metric-value   { font-size: 13px; font-weight: 600; font-variant-numeric: tabular-nums; }
.positive { color: var(--color-positive); }
.negative { color: var(--color-negative); }

/* Bar chart */
.bar-chart { display: flex; flex-direction: column; gap: 7px; }
.bar-row   { display: flex; align-items: center; gap: 8px; }
.bar-name  { font-size: 11px; color: var(--color-muted); width: 100px; flex-shrink: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.bar-track { flex: 1; display: flex; align-items: center; gap: 6px; }
.bar-fill  { height: 10px; border-radius: 3px; min-width: 3px; transition: width 0.4s ease; }
.bar-baseline { background: var(--color-muted); }
.bar-current  { background: var(--color-positive); }   /* FIX 2: rulebook strategy = green */
.bar-beat     { background: rgba(34,197,94,.4); }       /* alternative that beat = lighter green */
.bar-miss     { background: var(--color-accent); }
.bar-value    { font-size: 11px; font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--color-text); }

/* AI section */
.ai-section { display: flex; flex-direction: column; gap: 6px; }
.ai-loading { font-size: 12px; color: var(--color-muted); font-style: italic; }
.ai-text {
  font-size: 12px;
  line-height: 1.65;
  color: var(--color-text);
  background: var(--color-bg2);
  border-left: 3px solid var(--color-accent);
  padding: 10px 12px;
  border-radius: 0 7px 7px 0;
}
.ai-btn {
  align-self: flex-start;
  padding: 6px 12px;
  border-radius: 6px;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text);
  font-size: 12px;
  cursor: pointer;
}
.ai-btn:hover { background: var(--color-bg2); }

/* FIX 3: Action statement at bottom */
.action-statement {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-accent);
  border-radius: 0 7px 7px 0;
  color: var(--color-muted);
}

/* Confidence indicator */
.confidence-box { display: flex; gap: 10px; align-items: flex-start; padding: 8px 10px; border-radius: 7px; border: 1px solid transparent; }
.conf-strong    { background: rgba(34,197,94,.1);  border-color: rgba(34,197,94,.3); }
.conf-moderate  { background: rgba(234,179,8,.1);  border-color: rgba(234,179,8,.3); }
.conf-weak      { background: rgba(239,68,68,.08); border-color: rgba(239,68,68,.2); }
.confidence-left    { min-width: 80px; }
.confidence-label   { font-size: 10px; font-weight: 700; letter-spacing: 0.08em; color: var(--color-muted); margin-bottom: 2px; }
.confidence-value   { font-size: 13px; font-weight: 600; }
.confidence-reasons { display: flex; flex-direction: column; gap: 2px; justify-content: center; }
.confidence-reason  { font-size: 11px; color: var(--color-muted); }

/* Empty */
.empty-state { text-align: center; padding: 2rem 0.5rem; color: var(--color-muted); font-size: 13px; }
</style>