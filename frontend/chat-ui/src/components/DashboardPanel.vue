<template>
  <div class="db-root">

    <!-- ── Greeting ────────────────────────────────────────────── -->
    <div class="db-greeting">
      <div>
        <h1 class="db-title">{{ greeting }}, {{ displayName }}</h1>
        <p class="db-subtitle">Here's what needs your attention today.</p>
      </div>
      <button class="db-refresh" @click="loadAll" :disabled="loading" :title="loading ? 'Checking…' : 'Refresh'">
        <span class="db-refresh-icon" :class="{ 'db-spin': loading }">↻</span>
      </button>
    </div>

    <!-- ── Today's Action Card ────────────────────────────────── -->
    <div class="db-action" :class="actionCardClass">
      <!-- Loading -->
      <template v-if="loading">
        <div class="db-action-icon db-icon-muted">⟳</div>
        <div class="db-action-body">
          <p class="db-action-label">Checking market…</p>
          <p class="db-action-title">Scanning {{ screener?.totalScreened ?? 60 }} stocks</p>
          <p class="db-action-sub">This takes a few seconds.</p>
        </div>
      </template>

      <!-- Entry Signal — Track A (highest priority) -->
      <template v-else-if="screener && screener.oversoldCount > 0">
        <div class="db-action-icon db-icon-signal">🎯</div>
        <div class="db-action-body">
          <p class="db-action-label">What should I do today?</p>
          <p class="db-action-title">Entry signal — {{ entrySymbols }}</p>
          <p class="db-action-sub">
            RSI dropped below 30 and is turning up. Check the Screener tab and verify
            all Rulebook conditions before opening a paper trade.
          </p>
        </div>
        <button class="db-action-btn" @click="goToScreener">View Screener →</button>
      </template>

      <!-- Experimental Signal — Track B -->
      <template v-else-if="screener && screener.experimentalCount > 0">
        <div class="db-action-icon db-icon-exp">🧪</div>
        <div class="db-action-body">
          <p class="db-action-label">What should I do today?</p>
          <p class="db-action-title">Experimental signal — {{ experimentalSymbols }}</p>
          <p class="db-action-sub">
            RSI 30–40, turning up. Track B only — keep results separate from Track A.
            Review in the Screener before deciding.
          </p>
        </div>
        <button class="db-action-btn" @click="goToScreener">View Screener →</button>
      </template>

      <!-- No action needed -->
      <template v-else-if="screener">
        <div class="db-action-icon db-icon-ok">✓</div>
        <div class="db-action-body">
          <p class="db-action-label">What should I do today?</p>
          <p class="db-action-title">No action needed</p>
          <p class="db-action-sub">
            We're monitoring {{ screener.totalScreened }} stocks and none meet our entry rules right now.
            The system will alert you when one appears.
          </p>
        </div>
      </template>

      <!-- Backend offline / error -->
      <template v-else-if="screenerError">
        <div class="db-action-icon db-icon-err">⚠</div>
        <div class="db-action-body">
          <p class="db-action-label">Could not reach the backend</p>
          <p class="db-action-title">Make sure the server is running on port 60363</p>
          <p class="db-action-sub">Start with <code>dotnet run</code> in the MyAIAgent folder, then refresh.</p>
        </div>
        <button class="db-action-btn" @click="loadAll">Retry →</button>
      </template>

      <!-- Initial state — not yet loaded -->
      <template v-else>
        <div class="db-action-icon db-icon-muted">◌</div>
        <div class="db-action-body">
          <p class="db-action-label">What should I do today?</p>
          <p class="db-action-title">Press Refresh to check</p>
          <p class="db-action-sub">Click ↻ above to run a live scan of all 60 stocks.</p>
        </div>
      </template>
    </div>

    <!-- ── Metric tiles ────────────────────────────────────────── -->
    <div class="db-metrics">
      <div class="db-metric">
        <p class="db-metric-label">Open trades</p>
        <p class="db-metric-value">{{ portfolio ? portfolio.openTrades.length : '—' }}</p>
        <p class="db-metric-sub">
          {{ !portfolio ? '' : portfolio.openTrades.length === 0 ? 'Nothing at risk.' : 'Active positions.' }}
        </p>
      </div>
      <div class="db-metric">
        <p class="db-metric-label">Closed trades</p>
        <p class="db-metric-value">{{ portfolio ? portfolio.closedTrades.length : '—' }}</p>
        <p class="db-metric-sub">Completed trades.</p>
      </div>
      <div class="db-metric">
        <p class="db-metric-label">Win rate</p>
        <p class="db-metric-value" :class="winRateClass">{{ winRateDisplay }}</p>
        <p class="db-metric-sub">
          {{ !portfolio ? '' : portfolio.closedCount > 0 ? 'Of closed trades.' : 'No closed trades yet.' }}
        </p>
      </div>
      <div class="db-metric">
        <p class="db-metric-label">Stocks watched</p>
        <p class="db-metric-value">{{ screener?.totalScreened ?? 60 }}</p>
        <p class="db-metric-sub">Scanned every check.</p>
      </div>
    </div>

    <!-- ── Open positions (shown when trades are live) ──────────── -->
    <div v-if="portfolio && portfolio.openTrades.length > 0" class="db-section">
      <p class="db-section-title">Open positions — hold until RSI &gt; 60</p>
      <div v-for="t in portfolio.openTrades" :key="t.id" class="db-position">
        <div class="db-pos-badge">{{ t.symbol }}</div>
        <div class="db-pos-info">
          <span class="db-pos-symbol">{{ t.symbol }}</span>
          <span v-if="t.notes" class="db-pos-note">{{ t.notes }}</span>
        </div>
        <div class="db-pos-pnl" :class="(t.unrealizedPct ?? 0) >= 0 ? 'db-green' : 'db-red'">
          {{ t.unrealizedPct != null
              ? ((t.unrealizedPct > 0 ? '+' : '') + t.unrealizedPct + '%')
              : 'Pending' }}
        </div>
        <div class="db-pos-rule">Exit: RSI &gt; 60</div>
      </div>
    </div>

    <!-- ── Recent closed trade ────────────────────────────────── -->
    <div v-if="recentTrade" class="db-section">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;">
        <p class="db-section-title" style="margin-bottom:0;">Most recent closed trade</p>
        <span class="db-link" @click="goToPaper">See all trades →</span>
      </div>
      <div class="db-trade">
        <div class="db-trade-badge" :class="recentTrade.beatBenchmark ? 'db-badge-win' : 'db-badge-loss'">
          {{ recentTrade.symbol }}
        </div>
        <div class="db-trade-info">
          <p class="db-trade-symbol">{{ recentTrade.symbol }}</p>
          <p class="db-trade-sub">{{ recentTrade.notes || 'RSI entry — closed on recovery' }}</p>
        </div>
        <div class="db-trade-verdict" :class="recentTrade.beatBenchmark ? 'db-green' : 'db-red'">
          {{ recentTrade.beatBenchmark ? '✓ Beat B&H' : '✗ Lost to B&H' }}
        </div>
      </div>
    </div>

    <!-- ── AI Insight ─────────────────────────────────────────── -->
    <div class="db-insight">
      <div class="db-insight-header">
        <span class="db-insight-icon">✦</span>
        <span class="db-insight-label">AI insight</span>
      </div>
      <p class="db-insight-text">{{ insightText }}</p>
      <button class="db-insight-btn" @click="openChat">Ask AI →</button>
    </div>

    <!-- ── Progress bar ───────────────────────────────────────── -->
    <div class="db-progress">
      <div class="db-progress-header">
        <span class="db-progress-label">Validation progress</span>
        <span class="db-progress-value">{{ closedCount }}&nbsp;/&nbsp;20–30 trades</span>
      </div>
      <div class="db-progress-bar-track">
        <div class="db-progress-bar-fill" :style="{ width: progressPct + '%' }"></div>
      </div>
      <p class="db-progress-sub">{{ progressPct >= 100 ? 'Target reached — review findings.' : 'Accumulate 20–30 closed trades before drawing conclusions.' }}</p>
    </div>

  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'

const BASE = 'http://localhost:60363'

// ── State ──────────────────────────────────────────────────────
const loading       = ref(false)
const screener      = ref(null)
const portfolio     = ref(null)
const screenerError = ref(false)
const portfolioError = ref(false)

// ── Helpers ────────────────────────────────────────────────────
const userName = () => localStorage.getItem('userName') ?? 'test2'

const displayName = computed(() => {
  const u = localStorage.getItem('userName') ?? ''
  // Capitalise first letter, strip email domain if needed
  const name = u.includes('@') ? u.split('@')[0] : u
  return name.charAt(0).toUpperCase() + name.slice(1) || 'Bilal'
})

const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 12) return 'Good morning'
  if (h < 18) return 'Good afternoon'
  return 'Good evening'
})

// ── Screener-derived ───────────────────────────────────────────
const entrySymbols = computed(() => {
  if (!screener.value?.candidates) return ''
  return screener.value.candidates
    .filter(c => c.signalStatus === 'Entry Signal')
    .map(c => c.symbol)
    .join(', ')
})

const experimentalSymbols = computed(() => {
  if (!screener.value?.candidates) return ''
  return screener.value.candidates
    .filter(c => c.signalStatus === 'Experimental')
    .map(c => c.symbol)
    .join(', ')
})

const actionCardClass = computed(() => {
  if (loading.value) return 'db-action--loading'
  if (screenerError.value) return 'db-action--error'
  if (!screener.value) return 'db-action--idle'
  if (screener.value.oversoldCount > 0) return 'db-action--signal'
  if (screener.value.experimentalCount > 0) return 'db-action--experimental'
  return 'db-action--ok'
})

// ── Portfolio-derived ──────────────────────────────────────────
const recentTrade = computed(() => {
  const closed = portfolio.value?.closedTrades
  if (!closed || closed.length === 0) return null
  return closed[closed.length - 1]
})

const closedCount = computed(() => portfolio.value?.closedTrades?.length ?? 0)

const progressPct = computed(() => Math.min(Math.round((closedCount.value / 20) * 100), 100))

const winRateDisplay = computed(() => {
  if (!portfolio.value) return '—'
  if (!portfolio.value.closedCount || portfolio.value.closedCount === 0) return '—'
  return (portfolio.value.winRate ?? 0) + '%'
})

const winRateClass = computed(() => {
  if (!portfolio.value?.closedCount) return ''
  return (portfolio.value.winRate ?? 0) >= 50 ? 'db-green' : 'db-red'
})

// ── AI Insight (computed from state) ──────────────────────────
const insightText = computed(() => {
  if (screenerError.value)
    return 'Backend is offline. Start the server and refresh to get today\'s signal.'
  if (loading.value)
    return 'Scanning all 60 stocks for RSI signals…'
  if (screener.value?.oversoldCount > 0)
    return `${screener.value.oversoldCount} stock${screener.value.oversoldCount > 1 ? 's' : ''} crossed below RSI 30 and are turning up. Review the Screener, verify all 5 Rulebook conditions, then open a paper trade if they pass.`
  if (screener.value?.experimentalCount > 0)
    return 'A Track B experimental signal appeared. These are kept separate from validated Track A results — do not mix them.'
  if (portfolio.value?.openTrades?.length > 0)
    return `You have ${portfolio.value.openTrades.length} open position${portfolio.value.openTrades.length > 1 ? 's' : ''} being monitored. No action needed — hold until RSI crosses 60.`
  return 'No signals today. The system is scanning every stock in your list and will surface the next opportunity when it meets your validated rules.'
})

// ── Data loading ───────────────────────────────────────────────
async function loadScreener() {
  screenerError.value = false
  try {
    const res = await fetch(`${BASE}/api/screener/rsi-candidates`)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    screener.value = await res.json()
  } catch {
    screenerError.value = true
  }
}

async function loadPortfolio() {
  portfolioError.value = false
  try {
    const res = await fetch(`${BASE}/api/paper/${userName()}`)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    const text = await res.text()
    portfolio.value = JSON.parse(text)
  } catch {
    portfolioError.value = true
  }
}

async function loadAll() {
  loading.value = true
  await Promise.all([loadScreener(), loadPortfolio()])
  loading.value = false
}

// ── Navigation helpers ─────────────────────────────────────────
function goToScreener() {
  window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'screener' }))
}

function goToPaper() {
  window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'paper' }))
}

function openChat() {
  window.dispatchEvent(new CustomEvent('expand-chat'))
}

// ── Mount ──────────────────────────────────────────────────────
onMounted(loadAll)
</script>

<style scoped>
/* ── Root ── */
.db-root {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 2px 0 12px;
  font-size: 13px;
}

/* ── Greeting ── */
.db-greeting {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 2px;
}
.db-title {
  font-size: 17px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 2px;
}
.db-subtitle {
  font-size: 12px;
  color: var(--text-muted);
  margin: 0;
}
.db-refresh {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 50%;
  width: 30px;
  height: 30px;
  cursor: pointer;
  color: var(--text-secondary);
  font-size: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: color .15s, border-color .15s;
}
.db-refresh:hover:not(:disabled) { color: var(--accent); border-color: var(--accent); }
.db-refresh:disabled { opacity: .5; cursor: default; }
.db-refresh-icon { display: inline-block; transition: transform .5s; }
.db-spin { animation: db-spin 1s linear infinite; }
@keyframes db-spin { to { transform: rotate(360deg); } }

/* ── Action Card ── */
.db-action {
  border-radius: 10px;
  padding: 14px 16px;
  display: flex;
  align-items: flex-start;
  gap: 12px;
  flex-wrap: wrap;
  border: 1px solid transparent;
  transition: background .3s, border-color .3s;
}
.db-action--loading     { background: var(--bg-panel-item); border-color: var(--bg-panel-border); }
.db-action--idle        { background: var(--bg-panel-item); border-color: var(--bg-panel-border); }
.db-action--ok          { background: rgba(34,197,94,.08);  border-color: rgba(34,197,94,.25); }
.db-action--signal      { background: rgba(245,158,11,.08); border-color: rgba(245,158,11,.3);  }
.db-action--experimental{ background: rgba(139,92,246,.08); border-color: rgba(139,92,246,.3); }
.db-action--error       { background: rgba(239,68,68,.07);  border-color: rgba(239,68,68,.25);  }

.db-action-icon {
  font-size: 22px;
  flex-shrink: 0;
  margin-top: 1px;
  line-height: 1;
}
.db-icon-ok   { color: #22c55e; }
.db-icon-signal { }
.db-icon-exp  { }
.db-icon-err  { color: #ef4444; }
.db-icon-muted { color: var(--text-muted); }

.db-action-body { flex: 1; min-width: 0; }
.db-action-label {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .06em;
  color: var(--text-muted);
  margin: 0 0 3px;
}
.db-action--ok .db-action-label   { color: #22c55e; }
.db-action--signal .db-action-label { color: #f59e0b; }
.db-action--experimental .db-action-label { color: #8b5cf6; }
.db-action--error .db-action-label { color: #ef4444; }

.db-action-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px;
}
.db-action-sub {
  font-size: 12px;
  color: var(--text-secondary);
  margin: 0;
  line-height: 1.55;
}
.db-action-btn {
  flex-shrink: 0;
  align-self: center;
  background: transparent;
  border: 1px solid var(--bg-panel-border);
  border-radius: 6px;
  padding: 6px 12px;
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
  white-space: nowrap;
  transition: color .15s, border-color .15s;
}
.db-action-btn:hover { color: var(--accent); border-color: var(--accent); }

/* ── Metrics grid ── */
.db-metrics {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
}
@media (max-width: 480px) {
  .db-metrics { grid-template-columns: repeat(2, 1fr); }
}
.db-metric {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  padding: 10px 12px;
}
.db-metric-label {
  font-size: 11px;
  color: var(--text-muted);
  margin: 0 0 4px;
}
.db-metric-value {
  font-size: 22px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 3px;
  line-height: 1;
}
.db-metric-sub {
  font-size: 10px;
  color: var(--text-secondary);
  margin: 0;
}

/* ── Sections ── */
.db-section {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  padding: 12px 14px;
}
.db-section-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  margin: 0 0 8px;
  text-transform: uppercase;
  letter-spacing: .04em;
}
.db-link {
  font-size: 11px;
  color: var(--accent);
  cursor: pointer;
}
.db-link:hover { text-decoration: underline; }

/* ── Open positions ── */
.db-position {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 10px;
  background: var(--bg-panel-header);
  border-radius: 6px;
  margin-bottom: 6px;
}
.db-position:last-child { margin-bottom: 0; }
.db-pos-badge {
  width: 36px;
  height: 36px;
  border-radius: 7px;
  background: rgba(139,92,246,.15);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  color: #8b5cf6;
  flex-shrink: 0;
}
.db-pos-info { flex: 1; min-width: 0; }
.db-pos-symbol { font-size: 13px; font-weight: 600; color: var(--text-primary); display: block; }
.db-pos-note   { font-size: 10px; color: var(--text-muted); display: block; margin-top: 1px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.db-pos-pnl    { font-size: 13px; font-weight: 600; flex-shrink: 0; }
.db-pos-rule   { font-size: 10px; color: var(--text-muted); flex-shrink: 0; }

/* ── Recent trade ── */
.db-trade {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  background: var(--bg-panel-header);
  border-radius: 7px;
}
.db-trade-badge {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  flex-shrink: 0;
}
.db-badge-win  { background: rgba(34,197,94,.15);  color: #22c55e; }
.db-badge-loss { background: rgba(239,68,68,.12);  color: #ef4444; }
.db-trade-info { flex: 1; min-width: 0; }
.db-trade-symbol { font-size: 13px; font-weight: 600; color: var(--text-primary); margin: 0 0 2px; }
.db-trade-sub    { font-size: 11px; color: var(--text-muted); margin: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.db-trade-verdict { font-size: 12px; font-weight: 600; flex-shrink: 0; }

/* ── AI Insight ── */
.db-insight {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  padding: 12px 14px;
}
.db-insight-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 6px;
}
.db-insight-icon  { color: var(--accent); font-size: 14px; }
.db-insight-label { font-size: 13px; font-weight: 600; }
.db-insight-text  {
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.6;
  margin: 0 0 10px;
}
.db-insight-btn {
  background: transparent;
  border: 1px solid var(--bg-panel-border);
  border-radius: 6px;
  padding: 5px 12px;
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: color .15s, border-color .15s;
}
.db-insight-btn:hover { color: var(--accent); border-color: var(--accent); }

/* ── Progress bar ── */
.db-progress {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  padding: 12px 14px;
}
.db-progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.db-progress-label { font-size: 12px; font-weight: 600; color: var(--text-secondary); }
.db-progress-value { font-size: 12px; color: var(--text-muted); }
.db-progress-bar-track {
  height: 6px;
  background: var(--bg-panel-header);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 6px;
}
.db-progress-bar-fill {
  height: 100%;
  background: var(--accent);
  border-radius: 3px;
  transition: width .5s ease;
  min-width: 4px;
}
.db-progress-sub {
  font-size: 11px;
  color: var(--text-muted);
  margin: 0;
}

/* ── Utility colours ── */
.db-green { color: #22c55e; }
.db-red   { color: #ef4444; }
</style>
