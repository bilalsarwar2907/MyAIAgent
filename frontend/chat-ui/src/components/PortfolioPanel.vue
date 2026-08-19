<template>
  <div class="pf-panel">

    <!-- Header -->
    <div class="pf-header">
      <div class="pf-header-left">
        <span class="pf-title">💼 Real Holdings</span>
        <span class="pf-subtitle">Your actual purchased shares · P&L vs buy price</span>
      </div>
      <button class="pf-refresh-btn" :disabled="refreshing" @click="refreshPrices">
        <span v-if="refreshing" class="pf-spinner">⟳</span>
        <span v-else>⟳ Refresh</span>
      </button>
    </div>

    <!-- Identity note -->
    <div class="pf-identity-note">
      💡 This tracks <strong>real money</strong> you have invested.
      For paper trades and strategy testing, use the <strong>📋 Paper</strong> tab.
    </div>

    <!-- Summary bar -->
    <div v-if="items.length > 0" class="pf-summary">
      <div class="pf-summary-card">
        <div class="pf-summary-value">{{ fmtDollar(totalInvested) }}</div>
        <div class="pf-summary-label">Invested</div>
      </div>
      <!-- FIX 4: Current value — shows — until prices load, not a permanent dash -->
      <div class="pf-summary-card">
        <div class="pf-summary-value" :class="totalCurrentValue > 0 ? '' : 'pf-muted'">
          {{ totalCurrentValue > 0 ? fmtDollar(totalCurrentValue) : 'Refreshing…' }}
        </div>
        <div class="pf-summary-label">Current Value</div>
      </div>
      <div class="pf-summary-card">
        <div class="pf-summary-value" :class="totalCurrentValue > 0 ? (totalPnl >= 0 ? 'pf-green' : 'pf-red') : 'pf-muted'">
          {{ totalCurrentValue > 0 ? (totalPnl >= 0 ? '+' : '') + fmtDollar(totalPnl) : 'Refreshing…' }}
        </div>
        <div class="pf-summary-label">Total P&amp;L</div>
      </div>
      <div class="pf-summary-card" v-if="totalCurrentValue > 0">
        <div class="pf-summary-value pf-bold" :class="totalPnlPct >= 0 ? 'pf-green' : 'pf-red'">
          {{ (totalPnlPct >= 0 ? '+' : '') + totalPnlPct.toFixed(1) }}%
        </div>
        <div class="pf-summary-label">Return</div>
      </div>
    </div>

    <!-- Add form -->
    <div class="pf-section">
      <div class="pf-section-header" @click="showForm = !showForm">
        <span class="pf-section-title">➕ Add Holding</span>
        <span class="pf-chevron">{{ showForm ? '▲' : '▼' }}</span>
      </div>
      <div v-if="showForm" class="pf-form">
        <div class="pf-form-row">
          <div class="pf-field">
            <label class="pf-label">SYMBOL</label>
            <input class="pf-input pf-input--upper" v-model="form.symbol" placeholder="e.g. SLB" maxlength="10" />
          </div>
          <div class="pf-field">
            <label class="pf-label">SHARES</label>
            <input class="pf-input" v-model="form.shares" type="number" step="1" placeholder="e.g. 10" />
          </div>
        </div>
        <div class="pf-form-row">
          <div class="pf-field">
            <label class="pf-label">BUY PRICE ($)</label>
            <input class="pf-input" v-model="form.buyPrice" type="number" step="0.01" placeholder="e.g. 47.95" />
          </div>
          <div class="pf-field">
            <label class="pf-label">NOTE (optional)</label>
            <input class="pf-input" v-model="form.note" placeholder="e.g. Energy sector position" />
          </div>
        </div>
        <div v-if="formError" class="pf-form-error">⚠️ {{ formError }}</div>
        <div class="pf-form-actions">
          <button class="pf-btn pf-btn--primary" :disabled="formLoading" @click="addHolding">
            {{ formLoading ? 'Adding…' : '✓ Add Holding' }}
          </button>
          <button class="pf-btn pf-btn--ghost" @click="resetForm">Cancel</button>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-if="error" class="pf-error">⚠️ {{ error }}</div>
    <div v-if="loading" class="pf-empty">Loading holdings…</div>

    <div v-else-if="items.length === 0" class="pf-empty">
      <div class="pf-empty-icon">💼</div>
      <div class="pf-empty-title">No holdings yet</div>
      <div class="pf-empty-desc">Add a stock above to track your real money positions.</div>
    </div>

    <!-- Holdings list -->
    <div v-else class="pf-list">
      <div
        v-for="item in items" :key="item.id"
        class="pf-card"
        :class="[item.pnlPct > 0 ? 'pf-card--up' : item.pnlPct < 0 ? 'pf-card--down' : '', isExcluded(item) ? 'pf-card--excluded' : '']"
      >
        <!-- Top row -->
        <div class="pf-card-top">
          <div class="pf-card-left">
            <span class="pf-symbol" :class="{ 'pf-symbol--muted': isExcluded(item) }">{{ item.symbol }}</span>
            <span v-if="item.rsiStatus" class="pf-rsi-badge" :class="rsiBadgeClass(item.rsiStatus)">
              RSI {{ item.currentRsi }} · {{ item.rsiStatus }}
            </span>
          </div>
          <div class="pf-card-right">
              <!-- Trade State badge — canonical state from shared engine -->
              <span v-if="item.trendBucket != null" class="pf-state-badge" :class="getState(item).class">
                {{ getState(item).icon }} {{ getState(item).label }}
              </span>
              <span v-if="item.currentPrice" class="pf-current-price">{{ item.currentPrice }}</span>
              <span v-else class="pf-muted">—</span>
              <button class="pf-remove-btn" @click="confirmRemove(item)">✕</button>
            </div>
        </div>

        <!-- FIX 4: P&L row — always shown, states clearly when data is pending -->
        <div class="pf-pnl-row">
          <div class="pf-metric">
            <div class="pf-metric-value">{{ fmtDollar(item.shares * item.buyPrice) }}</div>
            <div class="pf-metric-label">Invested</div>
          </div>
          <div class="pf-metric">
            <div class="pf-metric-value" :class="item.currentValue ? '' : 'pf-muted'">
              {{ item.currentValue ? fmtDollar(item.currentValue) : '—' }}
            </div>
            <div class="pf-metric-label">Now Worth</div>
          </div>
          <div class="pf-metric">
            <div class="pf-metric-value" :class="item.pnl != null ? (item.pnl >= 0 ? 'pf-green' : 'pf-red') : 'pf-muted'">
              {{ item.pnl != null ? (item.pnl >= 0 ? '+' : '') + fmtDollar(item.pnl) : '—' }}
            </div>
            <div class="pf-metric-label">P&amp;L</div>
          </div>
          <div class="pf-metric">
            <div class="pf-metric-value pf-bold" :class="item.pnlPct != null ? (item.pnlPct >= 0 ? 'pf-green' : 'pf-red') : 'pf-muted'">
              {{ item.pnlPct != null ? (item.pnlPct >= 0 ? '+' : '') + item.pnlPct.toFixed(1) + '%' : '—' }}
            </div>
            <div class="pf-metric-label">Return</div>
          </div>
        </div>

        <div class="pf-detail">{{ item.shares }} shares @ ${{ Number(item.buyPrice).toFixed(2) }}</div>

        <!-- FIX 2+5: Accountability layer — was this a valid trade? -->
        <div v-if="isExcluded(item)" class="pf-accountability pf-accountability--invalid">
          ❌ This holding violates your strategy rules — strong trend excluded by Finding #1.
          Not aligned with RSI strategy. Monitor separately.
        </div>
        <div v-else-if="item.trendBucket != null && item.rsiStatus === 'Oversold'" class="pf-accountability pf-accountability--valid">
          ✅ Valid RSI setup — meets rulebook criteria (weak trend, oversold)
        </div>
        <div v-else-if="item.trendBucket != null" class="pf-accountability pf-accountability--neutral">
          📋 Strategy would not enter this position now — RSI not oversold. Hold and monitor.
        </div>

        <div v-if="item.note" class="pf-note">📝 {{ item.note }}</div>

        <div class="pf-card-actions">
          <button class="pf-btn pf-btn--sm" @click="analyzeStock(item.symbol)">📊 Analyze</button>
        </div>
      </div>
    </div>

    <!-- Confirm remove dialog -->
    <div v-if="removeTarget" class="pf-confirm-overlay" @click.self="removeTarget = null">
      <div class="pf-confirm-dialog">
        <div class="pf-confirm-title">Remove {{ removeTarget.symbol }}?</div>
        <div class="pf-confirm-detail">{{ removeTarget.shares }} shares @ ${{ Number(removeTarget.buyPrice).toFixed(2) }}</div>
        <div class="pf-confirm-note">This removes the holding from your real money tracker. It does not affect paper trades.</div>
        <div class="pf-confirm-actions">
          <button class="pf-btn pf-btn--danger" @click="removeHolding(removeTarget.id)">Remove</button>
          <button class="pf-btn pf-btn--ghost" @click="removeTarget = null">Cancel</button>
        </div>
      </div>
    </div>

    <div v-if="lastRefreshed" class="pf-last-refreshed">Prices updated {{ lastRefreshed }}</div>

  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { usePortfolioStore } from '@/stores/portfolioStore'
import { useAuthStore }      from '@/stores/authStore'
import { useChatStore }      from '@/stores/chatStore'
import { useTradeState }     from '@/composables/useTradeState'

const BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:60363'

const portfolioStore = usePortfolioStore()
const authStore      = useAuthStore()
const chatStore      = useChatStore()

// ── Trade State Engine ────────────────────────────────────────────────────
const { loadOpenTrades, getState } = useTradeState()

const items         = ref([])
const loading       = ref(false)
const refreshing    = ref(false)
const error         = ref(null)
const formError     = ref(null)
const formLoading   = ref(false)
const showForm      = ref(false)
const lastRefreshed = ref(null)
const removeTarget  = ref(null)
const form          = ref({ symbol: '', shares: '', buyPrice: '', note: '' })

// FIX 2: Rulebook validation — same rule as screener and watchlist
function isExcluded(item) {
  return item.trendBucket === 'Strong (>300%)'
}

const totalInvested     = computed(() => items.value.reduce((s, i) => s + i.shares * i.buyPrice, 0))
const totalCurrentValue = computed(() => items.value.reduce((s, i) => s + (i.currentValue ?? 0), 0))
const totalPnl          = computed(() => totalCurrentValue.value - totalInvested.value)
const totalPnlPct       = computed(() => totalInvested.value > 0 ? (totalPnl.value / totalInvested.value) * 100 : 0)

async function loadPortfolio() {
  loading.value = true; error.value = null
  try {
    await portfolioStore.loadPortfolio(authStore.userName)
    items.value = portfolioStore.items.map(i => ({
      ...i, currentPrice: null, currentValue: null, pnl: null, pnlPct: null, currentRsi: null, rsiStatus: null, trendBucket: null,
    }))
  } catch { error.value = 'Failed to load portfolio.' }
  finally { loading.value = false }
}

// FIX 4: refreshPrices — sole source of price data, no Alpha Vantage
async function refreshPrices() {
  if (items.value.length === 0) return
  refreshing.value = true
  for (const item of items.value) {
    try {
      const controller = new AbortController()
      const timeout    = setTimeout(() => controller.abort(), 15000)
      let res
      try { res = await fetch(`${BASE}/api/screener/rsi/${item.symbol}`, { signal: controller.signal }) }
      finally { clearTimeout(timeout) }
      if (!res.ok) { item.rsiStatus = 'Unavailable'; continue }
      const data = await res.json()
      if (data.error) { item.rsiStatus = 'Unavailable'; continue }

      item.currentRsi  = data.currentRsi
      item.trendBucket = data.trendBucket
      item.rsiStatus   = isExcluded(item) ? null : (data.currentRsi != null ? rsiStatus(data.currentRsi) : 'Unavailable')

      if (data.currentPrice != null) {
        const price       = parseFloat(data.currentPrice)
        item.currentPrice = '$' + price.toFixed(2)
        item.currentValue = price * item.shares
        item.pnl          = item.currentValue - item.shares * item.buyPrice
        item.pnlPct       = (item.pnl / (item.shares * item.buyPrice)) * 100
      }
    } catch { /* skip */ }
    await new Promise(r => setTimeout(r, 500))
  }
  lastRefreshed.value = new Date().toLocaleTimeString('en-DK', { hour: '2-digit', minute: '2-digit' })
  refreshing.value = false
}

async function addHolding() {
  formError.value = null
  if (!form.value.symbol.trim()) return (formError.value = 'Symbol is required.')
  if (!form.value.shares || Number(form.value.shares) <= 0) return (formError.value = 'Shares must be greater than 0.')
  if (!form.value.buyPrice || Number(form.value.buyPrice) <= 0) return (formError.value = 'Buy price must be greater than 0.')
  formLoading.value = true
  try {
    const ok = await portfolioStore.addHolding(authStore.userName, form.value.symbol.toUpperCase(), Number(form.value.shares), Number(form.value.buyPrice), form.value.note)
    if (ok) { resetForm(); await loadPortfolio(); await refreshPrices() }
    else { formError.value = portfolioStore.error ?? 'Failed to add holding.' }
  } finally { formLoading.value = false }
}

function confirmRemove(item) { removeTarget.value = item }
async function removeHolding(id) { await portfolioStore.removeHolding(id); items.value = items.value.filter(i => i.id !== id); removeTarget.value = null }
function resetForm() { showForm.value = false; formError.value = null; form.value = { symbol: '', shares: '', buyPrice: '', note: '' } }
function analyzeStock(symbol) { chatStore.sendMessage('analyze ' + symbol + ' and give me a detailed recommendation') }

function fmtDollar(n) { if (n == null) return '—'; return '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }
function rsiStatus(rsi) { if (rsi == null) return null; if (rsi < 30) return 'Oversold'; if (rsi > 70) return 'Overbought'; return 'Neutral' }
function rsiBadgeClass(status) { if (status === 'Oversold') return 'badge--oversold'; if (status === 'Overbought') return 'badge--overbought'; return 'badge--neutral' }

onMounted(async () => {
  await loadOpenTrades(authStore.userName)
  await loadPortfolio()
  await refreshPrices()
})
</script>

<style scoped>
.pf-panel { display: flex; flex-direction: column; gap: 10px; padding: 1rem; background: var(--bg-panel); color: var(--text-primary); min-width: 0; }
.pf-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.pf-header-left { display: flex; flex-direction: column; gap: 2px; }
.pf-title   { font-size: 15px; font-weight: 700; }
.pf-subtitle { font-size: 11px; color: var(--text-secondary); }
.pf-refresh-btn { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 7px; padding: 6px 13px; font-size: 12px; color: var(--text-primary); cursor: pointer; flex-shrink: 0; }
.pf-refresh-btn:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.pf-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.pf-spinner { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.pf-identity-note { font-size: 11px; color: var(--text-secondary); background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 7px; padding: 8px 12px; line-height: 1.5; }

.pf-summary { display: flex; gap: 8px; flex-wrap: wrap; }
.pf-summary-card { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 8px 14px; text-align: center; flex: 1; min-width: 70px; }
.pf-summary-value { font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.pf-summary-label { font-size: 9px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.06em; margin-top: 2px; }

.pf-section { display: flex; flex-direction: column; gap: 8px; }
.pf-section-header { display: flex; justify-content: space-between; align-items: center; background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 10px 14px; cursor: pointer; user-select: none; }
.pf-section-header:hover { border-color: var(--accent); }
.pf-section-title { font-size: 12px; font-weight: 700; }
.pf-chevron { font-size: 10px; color: var(--text-secondary); }
.pf-form { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 14px; display: flex; flex-direction: column; gap: 10px; }
.pf-form-row { display: flex; gap: 10px; flex-wrap: wrap; }
.pf-field { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 110px; }
.pf-label { font-size: 9px; font-weight: 700; letter-spacing: 0.08em; color: var(--text-secondary); }
.pf-input { background: var(--bg-panel); border: 1px solid var(--bg-panel-border); border-radius: 6px; padding: 6px 9px; font-size: 12px; color: var(--text-primary); height: 32px; }
.pf-input:focus { outline: none; border-color: var(--accent); }
.pf-input--upper { text-transform: uppercase; }
.pf-form-error { font-size: 11px; color: #ef4444; }
.pf-form-actions { display: flex; gap: 8px; }

.pf-btn { border-radius: 6px; padding: 7px 16px; font-size: 12px; font-weight: 600; cursor: pointer; border: none; }
.pf-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.pf-btn--primary { background: var(--accent); color: #fff; }
.pf-btn--primary:hover:not(:disabled) { opacity: 0.85; }
.pf-btn--ghost { background: var(--bg-panel-item); color: var(--text-secondary); border: 1px solid var(--bg-panel-border); }
.pf-btn--danger { background: rgba(239,68,68,.15); color: #ef4444; border: 1px solid rgba(239,68,68,.3); }
.pf-btn--danger:hover { background: rgba(239,68,68,.25); }
.pf-btn--sm { background: var(--bg-panel); border: 1px solid var(--bg-panel-border); color: var(--text-primary); padding: 5px 12px; font-size: 11px; border-radius: 5px; cursor: pointer; }
.pf-btn--sm:hover { border-color: var(--accent); color: var(--accent); }

.pf-error { background: rgba(239,68,68,.1); border: 1px solid rgba(239,68,68,.3); border-radius: 7px; padding: 8px 12px; font-size: 11px; color: #ef4444; }
.pf-empty { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 32px 16px; text-align: center; }
.pf-empty-icon { font-size: 28px; opacity: 0.4; }
.pf-empty-title { font-size: 13px; font-weight: 700; }
.pf-empty-desc { font-size: 11px; color: var(--text-secondary); line-height: 1.5; }

.pf-list { display: flex; flex-direction: column; gap: 8px; }
.pf-card { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 9px; padding: 11px 13px; display: flex; flex-direction: column; gap: 8px; }
.pf-card--up       { border-color: rgba(34,197,94,.3);  background: rgba(34,197,94,.03); }
.pf-card--down     { border-color: rgba(239,68,68,.25); background: rgba(239,68,68,.02); }
.pf-card--excluded { opacity: 0.7; border-color: rgba(239,68,68,.2) !important; }

.pf-card-top { display: flex; align-items: center; justify-content: space-between; gap: 6px; }
.pf-card-left  { display: flex; align-items: center; gap: 7px; flex-wrap: wrap; }
.pf-card-right { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
.pf-symbol { font-size: 15px; font-weight: 800; }
.pf-symbol--muted { color: var(--text-secondary); }
.pf-current-price { font-size: 13px; font-weight: 600; }

.pf-rsi-badge { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.04em; }
.badge--oversold   { background: rgba(34,197,94,.15); color: #22c55e; }
.badge--overbought { background: rgba(239,68,68,.12); color: #ef4444; }
.badge--neutral    { background: rgba(148,163,184,.1); color: var(--text-secondary); }

/* FIX 2: Rulebook badge */
.pf-rulebook-badge { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; }
.pf-rulebook-badge--valid   { background: rgba(34,197,94,.12); color: #22c55e; border: 1px solid rgba(34,197,94,.25); }
.pf-rulebook-badge--invalid { background: rgba(239,68,68,.1);  color: #ef4444; border: 1px solid rgba(239,68,68,.25); }

.pf-remove-btn { background: none; border: 1px solid var(--bg-panel-border); border-radius: 4px; color: var(--text-secondary); font-size: 10px; padding: 3px 7px; cursor: pointer; }
.pf-remove-btn:hover { border-color: #ef4444; color: #ef4444; }

.pf-pnl-row { display: flex; gap: 14px; flex-wrap: wrap; }
.pf-metric { display: flex; flex-direction: column; gap: 1px; }
.pf-metric-value { font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }
.pf-metric-label { font-size: 9px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.05em; }
.pf-detail { font-size: 11px; color: var(--text-secondary); }

/* FIX 2+5: Accountability layer */
.pf-accountability { font-size: 11px; border-radius: 5px; padding: 6px 10px; line-height: 1.4; }
.pf-accountability--invalid { background: rgba(239,68,68,.08); color: #ef4444; border: 1px solid rgba(239,68,68,.2); }
.pf-accountability--valid   { background: rgba(34,197,94,.08); color: #22c55e; border: 1px solid rgba(34,197,94,.2); }
.pf-accountability--neutral { background: var(--bg-panel); color: var(--text-secondary); border: 1px solid var(--bg-panel-border); }

.pf-note { font-size: 11px; color: var(--text-secondary); font-style: italic; padding: 4px 8px; background: var(--bg-panel); border-radius: 5px; border: 1px solid var(--bg-panel-border); }
.pf-card-actions { display: flex; gap: 6px; }

/* Trade State badge */
.pf-state-badge  { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.03em; }
.state--blocked  { background: rgba(239,68,68,.12);  color: #ef4444; }
.state--entry    { background: rgba(34,197,94,.15);  color: #22c55e; }
.state--in-trade { background: rgba(99,102,241,.15); color: #818cf8; }
.state--exit     { background: rgba(239,68,68,.15);  color: #ef4444; }
.state--idle     { background: rgba(148,163,184,.1); color: var(--text-secondary); }

/* Confirm dialog */
.pf-confirm-overlay { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 100; }
.pf-confirm-dialog { background: var(--bg-panel); border: 1px solid var(--bg-panel-border); border-radius: 12px; padding: 20px; width: 280px; display: flex; flex-direction: column; gap: 10px; }
.pf-confirm-title  { font-size: 14px; font-weight: 700; }
.pf-confirm-detail { font-size: 12px; color: var(--text-secondary); }
.pf-confirm-note   { font-size: 11px; color: var(--text-secondary); line-height: 1.4; }
.pf-confirm-actions { display: flex; gap: 8px; margin-top: 4px; }

.pf-green { color: #22c55e; }
.pf-red   { color: #ef4444; }
.pf-muted { color: var(--text-secondary); font-size: 11px; }
.pf-bold  { font-weight: 800; }
.pf-last-refreshed { font-size: 10px; color: var(--text-muted); text-align: center; padding-top: 4px; }
</style>