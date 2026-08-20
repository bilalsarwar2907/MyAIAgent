<template>
  <div class="wl-panel">

    <!-- Header -->
    <div class="wl-header">
      <div class="wl-header-left">
        <span class="wl-title">⭐ Watchlist</span>
        <span class="wl-subtitle">Monitor stocks for RSI signals · RSI &lt; 30 = oversold entry · RSI &gt; 60 = exit</span>
      </div>
      <button class="wl-refresh-btn" :disabled="refreshing" @click="refreshAll">
        <span v-if="refreshing" class="wl-spinner">⟳</span>
        <span v-else>⟳ Refresh</span>
      </button>
    </div>

    <!-- Add form -->
    <div class="wl-add-form">
      <input class="wl-input wl-input--upper" v-model="newSymbol" placeholder="Symbol e.g. SLB" maxlength="10" @keyup.enter="addStock" />
      <input class="wl-input" v-model="newNote" placeholder="Note (optional)" @keyup.enter="addStock" />
      <button class="wl-add-btn" :disabled="!newSymbol.trim()" @click="addStock">+ Add</button>
    </div>

    <!-- RSI info strip — always visible -->
    <div class="wl-info-strip">
      📐 <strong>RSI</strong> (Relative Strength Index) measures price momentum on a 0–100 scale.
      This platform watches for <strong>RSI &lt; 30</strong> (oversold) as a potential entry signal
      and <strong>RSI &gt; 60</strong> as the exit signal.
    </div>

    <!-- Error -->
    <div v-if="error" class="wl-error">⚠️ {{ error }}</div>

    <!-- Loading -->
    <div v-if="loading" class="wl-empty">Loading watchlist…</div>

    <!-- Empty -->
    <div v-else-if="items.length === 0" class="wl-purpose-card">
      <div class="wl-purpose-step">
        <span class="wl-step-num">1</span>
        <div><strong>Add a ticker</strong> — type any stock symbol (e.g. <code>SLB</code>, <code>IBM</code>, <code>XOM</code>) and press Enter or click + Add</div>
      </div>
      <div class="wl-purpose-step">
        <span class="wl-step-num">2</span>
        <div><strong>Refresh</strong> — fetches current RSI and 10-year Buy &amp; Hold trend for each stock you're watching</div>
      </div>
      <div class="wl-purpose-step">
        <span class="wl-step-num">3</span>
        <div><strong>Read the signal</strong> — RSI &lt; 30 = oversold, check Screener for entry · RSI &gt; 60 = recovered, consider closing paper trade</div>
      </div>
      <div class="wl-purpose-note">Stocks with a 10-year B&amp;H return &gt; 300% are automatically excluded by the Rulebook (Finding #1) and shown separately.</div>
    </div>

    <!-- FIX 3: Visually separate valid from excluded -->
    <div v-else class="wl-list">

      <!-- Valid opportunities section -->
      <div v-if="validItems.length > 0">
        <div class="wl-section-label wl-section-label--valid">✅ Valid Opportunities</div>
        <div
          v-for="item in validItems" :key="item.id"
          class="wl-card" :class="cardClass(item)"
        >
          <div class="wl-card-top">
            <div class="wl-card-left">
              <span class="wl-symbol">{{ item.symbol }}</span>
              <span v-if="item.inScreener" class="wl-screener-flag">🎯 In Screener</span>
            </div>
            <div class="wl-card-right">
              <!-- Trade State badge — the canonical state of this stock -->
              <span class="wl-state-badge" :class="getState(item).class">
                {{ getState(item).icon }} {{ getState(item).label }}
              </span>
              <span class="wl-rulebook-badge wl-rulebook-badge--valid">✅ Valid</span>
              <button class="wl-remove-btn" @click="removeStock(item.id)">✕</button>
            </div>
          </div>

          <div class="wl-card-metrics">
            <div class="wl-metric">
              <div class="wl-metric-value" :class="rsiClass(item.currentRsi)">
                {{ item.currentRsi != null ? item.currentRsi : '—' }}
              </div>
              <div class="wl-metric-label">RSI (14)</div>
            </div>
            <div class="wl-metric">
              <div class="wl-metric-value">{{ item.currentPrice ?? '—' }}</div>
              <div class="wl-metric-label">Price</div>
            </div>
            <div class="wl-metric" v-if="item.trendBucket">
              <div class="wl-metric-value wl-trend" :class="trendClass(item.trendBucket)">
                {{ trendShort(item.trendBucket) }}
              </div>
              <div class="wl-metric-label">10y Trend</div>
            </div>
          </div>

          <!-- State sublabel — single source of truth for what to do -->
          <div class="wl-state-sublabel" :class="getState(item).class + '-sub'">
            {{ getState(item).sublabel }}
          </div>

          <!-- LOOP: Decision flow shown only when actionable -->
          <div v-if="getState(item).id === 'entry' && item.inScreener" class="wl-action-guide wl-action-guide--go">
            🟢 Entry condition met — RSI oversold + in Screener
            <div class="wl-loop-steps">
              <span class="wl-loop-step wl-loop-step--done">✅ Alert fired</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--done">✅ Watchlist confirmed</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--now" @click="goToScreener">🎯 Validate in Screener</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--next" @click="goToPaper">📋 Open Paper Trade</span>
            </div>
          </div>
          <div v-else-if="getState(item).id === 'entry'" class="wl-action-guide wl-action-guide--watch">
            👁 RSI oversold — confirm via Screener before opening a paper trade
            <div class="wl-loop-steps">
              <span class="wl-loop-step wl-loop-step--now" @click="goToScreener">🎯 Check Screener →</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--next">📋 Paper Trade if valid</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--next">📊 Analytics tracks result</span>
            </div>
          </div>
          <div v-else-if="getState(item).id === 'exit'" class="wl-action-guide wl-action-guide--exit">
            🔴 Exit signal — RSI recovered above 60
            <div class="wl-loop-steps">
              <span class="wl-loop-step wl-loop-step--now" @click="goToPaper">📋 Close Paper Trade →</span>
              <span class="wl-loop-arrow">→</span>
              <span class="wl-loop-step wl-loop-step--next">📊 Analytics updates</span>
            </div>
          </div>

          <div v-if="item.note" class="wl-note">📝 {{ item.note }}</div>

          <div class="wl-card-actions">
            <button class="wl-btn wl-btn--analyze" @click="analyzeStock(item.symbol)">📊 Analyze</button>
            <button v-if="getState(item).id === 'entry' || getState(item).id === 'exit'"
              class="wl-btn wl-btn--trade" @click="getState(item).id === 'exit' ? goToPaper() : goToScreener()">
              {{ getState(item).id === 'exit' ? '📋 Close Trade' : '🎯 Check Screener' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Excluded / blocked section -->
      <div v-if="excludedItems.length > 0">
        <div class="wl-section-label wl-section-label--excluded">🚫 Excluded by Rulebook</div>
        <div
          v-for="item in excludedItems" :key="item.id"
          class="wl-card wl-card--excluded"
        >
          <div class="wl-card-top">
            <div class="wl-card-left">
              <span class="wl-symbol wl-symbol--muted">{{ item.symbol }}</span>
            </div>
            <div class="wl-card-right">
              <!-- FIX 1: EXCLUDED badge instead of NEUTRAL -->
              <span class="wl-status-badge badge--excluded">🚫 EXCLUDED</span>
              <!-- FIX 2: Rulebook validation badge -->
              <span class="wl-rulebook-badge wl-rulebook-badge--invalid">❌ Finding #1</span>
              <button class="wl-remove-btn" @click="removeStock(item.id)">✕</button>
            </div>
          </div>

          <div class="wl-card-metrics">
            <div class="wl-metric">
              <div class="wl-metric-value wl-muted">
                {{ item.currentRsi != null ? item.currentRsi : '—' }}
              </div>
              <div class="wl-metric-label">RSI (14)</div>
            </div>
            <div class="wl-metric">
              <div class="wl-metric-value wl-trend wl-red">Strong ⚠️</div>
              <div class="wl-metric-label">10y Trend</div>
            </div>
          </div>

          <!-- FIX 5: Clear action guidance — do not trade -->
          <div class="wl-action-guide wl-action-guide--blocked">
            🚫 Do not trade — strong trend violates rulebook (Finding #1)
          </div>

          <div v-if="item.note" class="wl-note">📝 {{ item.note }}</div>

          <div class="wl-card-actions">
            <button class="wl-btn wl-btn--analyze" @click="analyzeStock(item.symbol)">📊 Analyze</button>
          </div>
        </div>
      </div>

      <!-- Unloaded / still fetching -->
      <div v-if="pendingItems.length > 0">
        <div class="wl-section-label">⟳ Loading…</div>
        <div v-for="item in pendingItems" :key="item.id" class="wl-card wl-card--pending">
          <div class="wl-card-top">
            <span class="wl-symbol wl-muted">{{ item.symbol }}</span>
            <button class="wl-remove-btn" @click="removeStock(item.id)">✕</button>
          </div>
          <div class="wl-loading-text">Fetching RSI…</div>
        </div>
      </div>

    </div>

    <div v-if="lastRefreshed" class="wl-last-refreshed">Last refreshed {{ lastRefreshed }}</div>

  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useWatchlistStore } from '@/stores/watchlistStore'
import { useAuthStore } from '@/stores/authStore'
import { useChatStore } from '@/stores/chatStore'
import { useTradeState } from '@/composables/useTradeState'

const BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:60363'

const watchlistStore = useWatchlistStore()
const authStore      = useAuthStore()
const chatStore      = useChatStore()

// ── Trade State Engine ────────────────────────────────────────────────────
const { loadOpenTrades, getState } = useTradeState()

const items         = ref([])
const loading       = ref(false)
const refreshing    = ref(false)
const error         = ref(null)
const newSymbol     = ref('')
const newNote       = ref('')
const lastRefreshed = ref(null)

// ── Symbol normalisation ──────────────────────────────────────────────────
const SYMBOL_MAP = { 'GOOGLE': 'GOOGL', 'ALPHABET': 'GOOGL', 'FACEBOOK': 'META', 'TWITTER': 'X', 'MICROSOFT': 'MSFT' }
function normaliseSymbol(raw) {
  const upper = raw.trim().toUpperCase()
  return SYMBOL_MAP[upper] ?? upper
}

// ── FIX 2+3: Rulebook validation — the single source of truth ─────────────
// A stock is EXCLUDED if its 10-year B&H return > 300% (Finding #1)
// This is the same logic the screener uses. One rule, applied everywhere.
function isExcluded(item) {
  return item.trendBucket === 'Strong (>300%)'
}

// FIX 3: Computed lists — valid vs excluded vs still loading
const validItems    = computed(() => items.value.filter(i => i.rsiStatus != null && !isExcluded(i)))
const excludedItems = computed(() => items.value.filter(i => i.trendBucket != null && isExcluded(i)))
const pendingItems  = computed(() => items.value.filter(i => i.rsiStatus == null && i.trendBucket == null))

// ── Load ──────────────────────────────────────────────────────────────────
async function loadWatchlist() {
  loading.value = true
  error.value   = null
  try {
    await watchlistStore.loadWatchlist(authStore.userName)
    items.value = watchlistStore.items.map(i => ({ ...i, currentRsi: null, rsiStatus: null, trendBucket: null, inScreener: false }))
  } catch {
    error.value = 'Failed to load watchlist.'
  } finally {
    loading.value = false
  }
}

// ── Refresh ───────────────────────────────────────────────────────────────
async function refreshAll() {
  if (items.value.length === 0) return
  refreshing.value = true
  error.value      = null

  let screenerSymbols = new Set()
  try {
    const res = await fetch(`${BASE}/api/screener/rsi-candidates`)
    if (res.ok) {
      const data = await res.json()
      screenerSymbols = new Set(
        (data.candidates ?? []).filter(c => c.currentRsi != null && c.currentRsi < 30).map(c => c.symbol)
      )
    }
  } catch { /* best-effort */ }

  for (const item of items.value) {
    const fetchSymbol = normaliseSymbol(item.symbol)
    if (fetchSymbol !== item.symbol) item.symbol = fetchSymbol

    try {
      const controller = new AbortController()
      const timeout    = setTimeout(() => controller.abort(), 15000)
      let res
      try { res = await fetch(`${BASE}/api/screener/rsi/${fetchSymbol}`, { signal: controller.signal }) }
      finally { clearTimeout(timeout) }

      if (!res.ok) { item.rsiStatus = 'Unavailable'; continue }
      const data = await res.json()
      if (data.error) { item.rsiStatus = 'Unavailable'; continue }

      item.currentRsi  = data.currentRsi
      item.trendBucket = data.trendBucket
      item.inScreener  = screenerSymbols.has(fetchSymbol)
      // FIX 1: only set rsiStatus if not excluded — excluded stocks show EXCLUDED badge, not Neutral
      item.rsiStatus   = isExcluded(item) ? null : (data.currentRsi != null ? rsiStatus(data.currentRsi) : 'Unavailable')

      const storeItem = watchlistStore.items.find(i => i.id === item.id)
      if (storeItem?.currentPrice) {
        item.currentPrice  = storeItem.currentPrice
        item.change        = storeItem.change
        item.changePercent = storeItem.changePercent
      }
    } catch { /* skip */ }

    await new Promise(r => setTimeout(r, 500))
  }

  lastRefreshed.value = new Date().toLocaleTimeString('en-DK', { hour: '2-digit', minute: '2-digit' })
  refreshing.value = false
}

// ── Add / remove ──────────────────────────────────────────────────────────
async function addStock() {
  if (!newSymbol.value.trim()) return
  error.value = null
  const symbol = normaliseSymbol(newSymbol.value)
  const ok = await watchlistStore.addStock(authStore.userName, symbol, newNote.value)
  if (ok) {
    newSymbol.value = ''
    newNote.value   = ''
    await loadWatchlist()
    await refreshAll()
  } else {
    error.value = watchlistStore.error
  }
}

async function removeStock(id) {
  await watchlistStore.removeStock(id)
  items.value = items.value.filter(i => i.id !== id)
}

function analyzeStock(symbol) {
  chatStore.sendMessage('analyze ' + symbol + ' and give me a detailed recommendation')
  window.dispatchEvent(new CustomEvent('expand-chat'))
}
function goToScreener() { window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'screener' })) }
function goToPaper()    { window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'paper' })) }

// ── Helpers ───────────────────────────────────────────────────────────────
function rsiStatus(rsi) {
  if (rsi == null) return null
  if (rsi < 30) return 'Oversold'
  if (rsi > 70) return 'Overbought'
  return 'Neutral'
}
function rsiClass(rsi) {
  if (rsi == null) return 'wl-muted'
  if (rsi < 30)   return 'wl-green wl-bold'
  if (rsi > 70)   return 'wl-red wl-bold'
  return ''
}
function statusClass(status) {
  if (status === 'Oversold')   return 'badge--oversold'
  if (status === 'Overbought') return 'badge--overbought'
  return 'badge--neutral'
}
function trendClass(bucket) {
  if (bucket === 'Strong (>300%)') return 'wl-red'
  if (bucket === 'Weak (<100%)')   return 'wl-green'
  return 'wl-muted'
}
function trendShort(bucket) {
  if (!bucket) return '—'
  if (bucket.includes('Strong')) return 'Strong ⚠️'
  if (bucket.includes('Weak'))   return 'Weak ✓'
  return 'Medium'
}
function cardClass(item) {
  if (item.rsiStatus === 'Oversold')   return 'wl-card--oversold'
  if (item.rsiStatus === 'Overbought') return 'wl-card--overbought'
  return ''
}
function changeClass(change) {
  if (!change) return 'wl-muted'
  return parseFloat(change) >= 0 ? 'wl-green' : 'wl-red'
}

onMounted(async () => {
  await loadOpenTrades(authStore.userName)
  await loadWatchlist()
  await refreshAll()
})
</script>

<style scoped>
.wl-panel { display: flex; flex-direction: column; gap: 10px; padding: 1rem; background: var(--bg-panel); color: var(--text-primary); min-width: 0; }

/* Header */
.wl-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.wl-header-left { display: flex; flex-direction: column; gap: 2px; }
.wl-title   { font-size: 15px; font-weight: 700; }
.wl-subtitle { font-size: 11px; color: var(--text-secondary); }
.wl-refresh-btn { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 7px; padding: 6px 13px; font-size: 12px; color: var(--text-primary); cursor: pointer; flex-shrink: 0; }
.wl-refresh-btn:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.wl-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.wl-spinner { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* Add form */
.wl-add-form { display: flex; flex-direction: column; gap: 6px; }
.wl-input { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 6px; padding: 7px 10px; font-size: 12px; color: var(--text-primary); height: 32px; }
.wl-input:focus { outline: none; border-color: var(--accent); }
.wl-input--upper { text-transform: uppercase; }
.wl-input::placeholder { color: var(--text-muted); }
.wl-add-btn { background: var(--accent); color: #fff; border: none; border-radius: 6px; padding: 7px; font-size: 12px; font-weight: 600; cursor: pointer; }
.wl-add-btn:hover:not(:disabled) { opacity: 0.85; }
.wl-add-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* Error / empty */
.wl-error { background: rgba(239,68,68,.1); border: 1px solid rgba(239,68,68,.3); border-radius: 7px; padding: 8px 12px; font-size: 11px; color: #ef4444; }
.wl-empty { display: flex; flex-direction: column; align-items: center; gap: 6px; padding: 32px 16px; text-align: center; }
.wl-empty-icon { font-size: 28px; opacity: 0.4; }
.wl-empty-title { font-size: 13px; font-weight: 700; }
.wl-empty-desc { font-size: 11px; color: var(--text-secondary); line-height: 1.5; }

/* FIX 3: Section labels */
.wl-section-label { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.07em; padding: 6px 2px 4px; color: var(--text-secondary); }
.wl-section-label--valid    { color: #22c55e; }
.wl-section-label--excluded { color: #ef4444; opacity: 0.8; }

/* Card list */
.wl-list { display: flex; flex-direction: column; gap: 6px; }
.wl-card { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 9px; padding: 11px 13px; display: flex; flex-direction: column; gap: 8px; transition: border-color 0.15s; }
.wl-card--oversold  { border-color: rgba(34,197,94,.4); background: rgba(34,197,94,.04); }
.wl-card--overbought { border-color: rgba(239,68,68,.3); background: rgba(239,68,68,.03); }
/* FIX 3: Excluded cards are dimmed */
.wl-card--excluded { opacity: 0.65; border-color: rgba(239,68,68,.2); background: rgba(239,68,68,.02); }
.wl-card--pending  { opacity: 0.5; }

/* Card top */
.wl-card-top { display: flex; align-items: center; justify-content: space-between; gap: 6px; }
.wl-card-left  { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.wl-card-right { display: flex; align-items: center; gap: 5px; flex-wrap: wrap; justify-content: flex-end; }
.wl-symbol { font-size: 15px; font-weight: 800; }
.wl-symbol--muted { color: var(--text-secondary); }
.wl-screener-flag { font-size: 9px; font-weight: 700; background: rgba(233,69,96,.15); color: var(--accent); border: 1px solid rgba(233,69,96,.3); border-radius: 4px; padding: 2px 6px; }

/* Status badges */
.wl-status-badge { font-size: 9px; font-weight: 700; padding: 3px 8px; border-radius: 4px; letter-spacing: 0.04em; text-transform: uppercase; }
.badge--oversold   { background: rgba(34,197,94,.15); color: #22c55e; }
.badge--overbought { background: rgba(239,68,68,.12); color: #ef4444; }
.badge--neutral    { background: rgba(148,163,184,.1); color: var(--text-secondary); }
/* FIX 1: Excluded badge */
.badge--excluded   { background: rgba(239,68,68,.12); color: #ef4444; }

/* FIX 2: Rulebook validation badge */
.wl-rulebook-badge { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; letter-spacing: 0.03em; }
.wl-rulebook-badge--valid   { background: rgba(34,197,94,.12); color: #22c55e; border: 1px solid rgba(34,197,94,.25); }
.wl-rulebook-badge--invalid { background: rgba(239,68,68,.1);  color: #ef4444; border: 1px solid rgba(239,68,68,.25); }

.wl-remove-btn { background: none; border: 1px solid var(--bg-panel-border); border-radius: 4px; color: var(--text-secondary); font-size: 10px; padding: 3px 7px; cursor: pointer; }
.wl-remove-btn:hover { border-color: #ef4444; color: #ef4444; }

/* Metrics row */
.wl-card-metrics { display: flex; gap: 14px; flex-wrap: wrap; }
.wl-metric { display: flex; flex-direction: column; gap: 1px; }
.wl-metric-value { font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; line-height: 1.1; }
.wl-metric-label { font-size: 9px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.05em; }
.wl-trend { font-size: 12px; }

/* FIX 5: Action guidance */
.wl-action-guide { font-size: 11px; border-radius: 5px; padding: 6px 10px; line-height: 1.4; font-weight: 500; }
.wl-action-guide--go      { background: rgba(34,197,94,.1);  color: #22c55e; border: 1px solid rgba(34,197,94,.25); }
.wl-action-guide--watch   { background: rgba(234,179,8,.08); color: #eab308; border: 1px solid rgba(234,179,8,.2); }
.wl-action-guide--wait    { background: var(--bg-panel); color: var(--text-secondary); border: 1px solid var(--bg-panel-border); }
.wl-action-guide--blocked { background: rgba(239,68,68,.08); color: #ef4444; border: 1px solid rgba(239,68,68,.2); }

/* Trade State badge */
.wl-state-badge { font-size: 9px; font-weight: 700; padding: 3px 8px; border-radius: 4px; letter-spacing: 0.03em; text-transform: uppercase; }
.state--blocked  { background: rgba(239,68,68,.12);  color: #ef4444; }
.state--entry    { background: rgba(34,197,94,.15);  color: #22c55e; }
.state--in-trade { background: rgba(99,102,241,.15); color: #818cf8; }
.state--exit     { background: rgba(239,68,68,.15);  color: #ef4444; }
.state--idle     { background: rgba(148,163,184,.1); color: var(--text-secondary); }

/* State sublabel */
.wl-state-sublabel { font-size: 10px; color: var(--text-secondary); padding: 0 1px; line-height: 1.4; }
.state--entry-sub    { color: #22c55e; }
.state--in-trade-sub { color: #818cf8; }
.state--exit-sub     { color: #ef4444; }

.wl-action-guide--exit { background: rgba(239,68,68,.08); color: #ef4444; border: 1px solid rgba(239,68,68,.2); }
.wl-loop-steps { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; margin-top: 5px; }
.wl-loop-step  { font-size: 10px; font-weight: 600; padding: 2px 8px; border-radius: 4px; white-space: nowrap; }
.wl-loop-arrow { font-size: 10px; color: var(--text-muted); }
.wl-loop-step--done { background: rgba(34,197,94,.12);  color: #22c55e; }
.wl-loop-step--now  { background: var(--accent); color: #fff; cursor: pointer; }
.wl-loop-step--now:hover { opacity: 0.85; }
.wl-loop-step--next { background: var(--bg-panel-border); color: var(--text-secondary); }

.wl-note { font-size: 11px; color: var(--text-secondary); font-style: italic; padding: 4px 8px; background: var(--bg-panel); border-radius: 5px; border: 1px solid var(--bg-panel-border); }

/* Actions */
.wl-card-actions { display: flex; gap: 6px; }
.wl-btn { flex: 1; padding: 6px 10px; border-radius: 5px; font-size: 11px; font-weight: 600; cursor: pointer; border: 1px solid var(--bg-panel-border); background: var(--bg-panel); color: var(--text-primary); transition: background 0.15s, border-color 0.15s; }
.wl-btn--analyze:hover { border-color: var(--accent); color: var(--accent); }
.wl-btn--trade { background: rgba(34,197,94,.1); border-color: rgba(34,197,94,.3); color: #22c55e; }
.wl-btn--trade:hover { background: rgba(34,197,94,.2); }

/* Colour helpers */
.wl-green { color: #22c55e; }
.wl-red   { color: #ef4444; }
.wl-muted { color: var(--text-secondary); }
.wl-bold  { font-weight: 800; }

.wl-last-refreshed { font-size: 10px; color: var(--text-muted); text-align: center; padding-top: 4px; }

/* RSI info strip */
.wl-info-strip {
  font-size: 11px;
  line-height: 1.5;
  padding: 8px 12px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-left: 3px solid #818cf8;
  border-radius: 0 7px 7px 0;
  color: var(--text-secondary);
}
.wl-info-strip strong { color: #818cf8; }

/* Purpose card (empty state) */
.wl-purpose-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 18px 16px;
  background: var(--bg-panel-item);
  border-radius: 8px;
  border: 1px solid var(--bg-panel-border);
}
.wl-purpose-step {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  font-size: 12px;
  line-height: 1.55;
}
.wl-step-num {
  flex-shrink: 0;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--accent);
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: 1px;
}
.wl-purpose-note {
  font-size: 11px;
  color: var(--text-secondary);
  border-top: 1px solid var(--bg-panel-border);
  padding-top: 10px;
  line-height: 1.5;
}
</style>