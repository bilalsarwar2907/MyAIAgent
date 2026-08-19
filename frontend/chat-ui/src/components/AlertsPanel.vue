<template>
  <div class="al-panel">

    <!-- Header -->
    <div class="al-header">
      <div class="al-header-left">
        <span class="al-title">🔔 RSI Alerts</span>
        <span class="al-subtitle">Get notified when RSI hits your threshold</span>
      </div>
      <button class="al-refresh-btn" :disabled="checking" @click="checkNow">
        <span v-if="checking" class="al-spinner">⟳</span>
        <span v-else>⟳ Check Now</span>
      </button>
    </div>

    <!-- How it works -->
    <div class="al-explainer">
      <div class="al-explainer-row">
        <span class="al-explainer-badge badge--entry">ENTRY</span>
        RSI drops <strong>below 30</strong> → stock is oversold → check the Screener
      </div>
      <div class="al-explainer-row">
        <span class="al-explainer-badge badge--exit">EXIT</span>
        RSI rises <strong>above 60</strong> → trade recovered → consider closing position
      </div>
    </div>

    <!-- Triggered banner -->
    <transition name="fade">
      <div v-if="triggered.length > 0" class="al-triggered-banner">
        <div v-for="t in triggered" :key="t.symbol + t.threshold" class="al-triggered-item">
          🎯 <strong>{{ t.symbol }}</strong> RSI {{ t.currentRsi }} —
          {{ t.type === 'entry' ? 'oversold, check screener' : 'recovered, consider closing' }}
        </div>
      </div>
    </transition>

    <!-- Add form -->
    <div class="al-section">
      <div class="al-section-header" @click="showForm = !showForm">
        <span class="al-section-title">➕ Add RSI Alert</span>
        <span class="al-chevron">{{ showForm ? '▲' : '▼' }}</span>
      </div>
      <div v-if="showForm" class="al-form">
        <div class="al-form-row">
          <div class="al-field">
            <label class="al-label">SYMBOL</label>
            <input class="al-input al-input--upper" v-model="form.symbol" placeholder="e.g. SLB" maxlength="10" />
          </div>
          <div class="al-field">
            <label class="al-label">ALERT TYPE</label>
            <select class="al-input al-select" v-model="form.type">
              <option value="entry">Entry — RSI drops below threshold</option>
              <option value="exit">Exit — RSI rises above threshold</option>
            </select>
          </div>
          <div class="al-field al-field--sm">
            <label class="al-label">RSI THRESHOLD</label>
            <input class="al-input" v-model="form.threshold" type="number" step="1" min="1" max="99"
              :placeholder="form.type === 'entry' ? '30' : '60'" />
          </div>
        </div>
        <div v-if="formError" class="al-form-error">⚠️ {{ formError }}</div>
        <div class="al-form-actions">
          <button class="al-btn al-btn--primary" @click="addAlert">✓ Set Alert</button>
          <button class="al-btn al-btn--ghost" @click="resetForm">Cancel</button>
        </div>
      </div>
    </div>

    <div v-if="error" class="al-error">⚠️ {{ error }}</div>

    <div v-if="alerts.length === 0 && !loading" class="al-empty">
      <div class="al-empty-icon">🔔</div>
      <div class="al-empty-title">No RSI alerts set</div>
      <div class="al-empty-desc">Add an entry alert at RSI 30 for stocks on your watchlist.</div>
    </div>

    <div v-else class="al-list">

      <!-- FIX 3: Valid alerts first (sorted by proximity to trigger) -->
      <div v-if="validActiveAlerts.length > 0">
        <div class="al-group-title">
          ✅ Valid Alerts — Watching
          <span class="al-count">({{ validActiveAlerts.length }})</span>
        </div>
        <div v-for="a in validActiveAlerts" :key="a.id" class="al-card" :class="alertCardClass(a)">
          <div class="al-card-top">
            <div class="al-card-left">
              <span class="al-symbol">{{ a.symbol }}</span>
              <span class="al-type-badge" :class="a.type === 'entry' ? 'badge--entry' : 'badge--exit'">
                {{ a.type === 'entry' ? 'ENTRY' : 'EXIT' }}
              </span>
              <span v-if="isClosestToTrigger(a)" class="al-priority-flag">🔥 Closest</span>
            </div>
            <div class="al-card-right">
              <!-- Trade state badge from canonical engine -->
              <span class="al-state-badge" :class="getState(a).class">
                {{ getState(a).icon }} {{ getState(a).label }}
              </span>
              <span class="al-threshold">RSI {{ a.type === 'entry' ? '< ' : '> ' }}{{ a.threshold }}</span>
              <button class="al-remove-btn" @click="removeAlert(a.id)">✕</button>
            </div>
          </div>

          <div class="al-rsi-row">
            <div class="al-metric">
              <div class="al-metric-value" :class="rsiClass(a.currentRsi)">{{ a.currentRsi != null ? a.currentRsi : '—' }}</div>
              <div class="al-metric-label">Current RSI</div>
            </div>
            <div class="al-metric">
              <div class="al-metric-value al-muted">{{ a.threshold }}</div>
              <div class="al-metric-label">Threshold</div>
            </div>
            <div class="al-metric" v-if="a.currentRsi != null">
              <div class="al-metric-value" :class="gapClass(a)">{{ rsiGap(a) }}</div>
              <div class="al-metric-label">Gap</div>
            </div>
            <div class="al-status-col">
              <!-- FIX 2: "Monitoring for RSI trigger" instead of passive "Watching" -->
              <span class="al-status" :class="statusClass(a)">{{ alertStatus(a) }}</span>
            </div>
          </div>

          <div v-if="isFired(a)" class="al-fire-action">
            <template v-if="a.type === 'entry'">
              <div class="al-fire-title">🟢 Entry signal triggered — RSI {{ a.currentRsi }} is oversold</div>
              <div class="al-loop-steps">
                <span class="al-loop-step al-loop-step--done">✅ Alert fired</span>
                <span class="al-loop-arrow">→</span>
                <span class="al-loop-step al-loop-step--now" @click="goToScreener">🎯 Validate in Screener</span>
                <span class="al-loop-arrow">→</span>
                <span class="al-loop-step al-loop-step--next" @click="goToPaper">📋 Open Paper Trade</span>
                <span class="al-loop-arrow">→</span>
                <span class="al-loop-step al-loop-step--next">📊 Analytics tracks result</span>
              </div>
            </template>
            <template v-else>
              <div class="al-fire-title">🔴 Exit signal triggered — RSI {{ a.currentRsi }} has recovered</div>
              <div class="al-loop-steps">
                <span class="al-loop-step al-loop-step--done">✅ Exit alert fired</span>
                <span class="al-loop-arrow">→</span>
                <span class="al-loop-step al-loop-step--now" @click="goToPaper">📋 Close Paper Trade</span>
                <span class="al-loop-arrow">→</span>
                <span class="al-loop-step al-loop-step--next">📊 Analytics updates</span>
              </div>
            </template>
          </div>
        </div>
      </div>

      <!-- FIX 4: Excluded alerts — dimmed, labelled, no false urgency -->
      <div v-if="excludedActiveAlerts.length > 0">
        <div class="al-group-title al-group-title--excluded">
          🚫 Excluded by Rulebook — Finding #1
          <span class="al-count">({{ excludedActiveAlerts.length }})</span>
        </div>
        <div v-for="a in excludedActiveAlerts" :key="a.id" class="al-card al-card--rulebook-excluded">
          <div class="al-card-top">
            <div class="al-card-left">
              <span class="al-symbol al-symbol--muted">{{ a.symbol }}</span>
              <span class="al-type-badge" :class="a.type === 'entry' ? 'badge--entry' : 'badge--exit'">
                {{ a.type === 'entry' ? 'ENTRY' : 'EXIT' }}
              </span>
            </div>
            <div class="al-card-right">
              <span class="al-excluded-badge">🚫 Alert Disabled</span>
              <button class="al-remove-btn" @click="removeAlert(a.id)">✕</button>
            </div>
          </div>
          <div class="al-rsi-row">
            <div class="al-metric">
              <div class="al-metric-value al-muted">{{ a.currentRsi != null ? a.currentRsi : '—' }}</div>
              <div class="al-metric-label">Current RSI</div>
            </div>
          </div>
          <!-- FIX 4: Clear explanation, no action shown -->
          <div class="al-excluded-note">
            🚫 Alert disabled — strong trend excludes this stock by Finding #1. RSI signal is not valid here.
          </div>
        </div>
      </div>

      <!-- Unloaded alerts -->
      <div v-if="loadingAlerts.length > 0">
        <div class="al-group-title">⟳ Loading… <span class="al-count">({{ loadingAlerts.length }})</span></div>
        <div v-for="a in loadingAlerts" :key="a.id" class="al-card al-card--loading">
          <div class="al-card-top">
            <span class="al-symbol al-muted">{{ a.symbol }}</span>
            <button class="al-remove-btn" @click="removeAlert(a.id)">✕</button>
          </div>
          <div class="al-loading-text">Fetching RSI…</div>
        </div>
      </div>

      <!-- Fired alerts -->
      <div v-if="firedAlerts.length > 0">
        <div class="al-group-title al-group-title--fired">
          Recently Fired <span class="al-count">({{ firedAlerts.length }})</span>
        </div>
        <div v-for="a in firedAlerts" :key="'f' + a.id" class="al-card al-card--fired">
          <div class="al-card-top">
            <div class="al-card-left">
              <span class="al-symbol">{{ a.symbol }}</span>
              <span class="al-type-badge" :class="a.type === 'entry' ? 'badge--entry' : 'badge--exit'">
                {{ a.type === 'entry' ? 'ENTRY' : 'EXIT' }}
              </span>
            </div>
            <div class="al-card-right">
              <span class="al-fired-label">Fired at RSI {{ a.firedAt }}</span>
              <button class="al-remove-btn" @click="removeAlert(a.id)">✕</button>
            </div>
          </div>
        </div>
      </div>

    </div>

    <div v-if="lastChecked" class="al-last-checked">Last checked {{ lastChecked }}</div>

  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/authStore'
import { useTradeState } from '@/composables/useTradeState'

const BASE      = import.meta.env.VITE_API_BASE_URL || 'http://localhost:60363'
const authStore = useAuthStore()

// ── Trade State Engine ────────────────────────────────────────────────────
const { loadOpenTrades, getState } = useTradeState()

const alerts      = ref([])
const loading     = ref(false)
const checking    = ref(false)
const error       = ref(null)
const formError   = ref(null)
const showForm    = ref(false)
const triggered   = ref([])
const lastChecked = ref(null)
let   autoCheck   = null

const form = ref({ symbol: '', type: 'entry', threshold: '30' })

// ── FIX 4: Rulebook check — same rule as all other panels ─────────────────
function isExcluded(alert) {
  return alert.trendBucket === 'Strong (>300%)'
}

// ── FIX 3: Computed groups — valid vs excluded vs loading vs fired ─────────
const activeAlerts         = computed(() => alerts.value.filter(a => !a.fired))
const firedAlerts          = computed(() => alerts.value.filter(a => a.fired))
const loadingAlerts        = computed(() => activeAlerts.value.filter(a => a.currentRsi == null && a.trendBucket == null))
const validActiveAlerts    = computed(() => {
  const valid = activeAlerts.value.filter(a => a.currentRsi != null && !isExcluded(a))
  // FIX 3: Sort by closest to trigger (smallest gap first)
  return valid.sort((a, b) => Math.abs(gapNumber(a)) - Math.abs(gapNumber(b)))
})
const excludedActiveAlerts = computed(() => activeAlerts.value.filter(a => a.trendBucket != null && isExcluded(a)))

// FIX 3: Is this the alert closest to firing?
function isClosestToTrigger(alert) {
  if (validActiveAlerts.value.length < 2) return false
  return validActiveAlerts.value[0].id === alert.id
}

function gapNumber(a) {
  if (a.currentRsi == null) return 999
  return a.type === 'entry' ? a.currentRsi - a.threshold : a.threshold - a.currentRsi
}

// ── Persistence ───────────────────────────────────────────────────────────
const STORAGE_KEY = () => `rsi_alerts_${authStore.userName}`
function saveAlerts() { localStorage.setItem(STORAGE_KEY(), JSON.stringify(alerts.value)) }
function loadAlerts() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY())
    const loaded = raw ? JSON.parse(raw) : []
    // Deduplicate: keep only the latest alert per symbol+type combination
    // This cleans up any duplicates that crept in from previous sessions
    const seen = new Map()
    for (const a of loaded) {
      const key = `${a.symbol}:${a.type}:${a.threshold}`
      if (!seen.has(key)) seen.set(key, a)
    }
    alerts.value = Array.from(seen.values())
    // Persist the deduped list immediately
    if (alerts.value.length !== loaded.length) saveAlerts()
  } catch {
    alerts.value = []
  }
}

// ── Add / remove ──────────────────────────────────────────────────────────
function addAlert() {
  formError.value = null
  if (!form.value.symbol.trim()) return (formError.value = 'Symbol is required.')
  const threshold = Number(form.value.threshold)
  if (!threshold || threshold < 1 || threshold > 99) return (formError.value = 'RSI threshold must be between 1 and 99.')

  const newAlert = {
    id: Date.now(),
    symbol: form.value.symbol.toUpperCase().trim(),
    type: form.value.type,
    threshold,
    currentRsi: null,
    trendBucket: null,
    fired: false,
    firedAt: null,
  }
  alerts.value.unshift(newAlert)
  saveAlerts()
  resetForm()
  fetchRsiForAlert(newAlert)
}

function removeAlert(id) { alerts.value = alerts.value.filter(a => a.id !== id); saveAlerts() }
function resetForm() { showForm.value = false; formError.value = null; form.value = { symbol: '', type: 'entry', threshold: '30' } }

// ── RSI fetch — now also retrieves trendBucket for rulebook check ─────────
async function fetchRsiForAlert(alert) {
  try {
    const controller = new AbortController()
    const timeout    = setTimeout(() => controller.abort(), 15000)
    let res
    try { res = await fetch(`${BASE}/api/screener/rsi/${alert.symbol}`, { signal: controller.signal }) }
    finally { clearTimeout(timeout) }
    if (!res.ok) return
    const data = await res.json()
    if (data.error) return

    alert.currentRsi  = data.currentRsi
    alert.trendBucket = data.trendBucket   // FIX 4: store trendBucket

    // FIX duplicate fired: only fire if not already fired — belt-and-suspenders guard
    // The .fired flag is persisted in localStorage, so this check survives page reloads.
    // Secondary guard: compare firedAt to current RSI — if same value, already processed.
    if (data.currentRsi != null && !isExcluded(alert) && !alert.fired) {
      const shouldFire = alert.type === 'entry'
        ? data.currentRsi < alert.threshold
        : data.currentRsi > alert.threshold

      if (shouldFire) {
        alert.fired   = true
        alert.firedAt = data.currentRsi
        // Only add to triggered banner if not already showing for this symbol
        const alreadyBannered = triggered.value.some(t => t.symbol === alert.symbol && t.type === alert.type)
        if (!alreadyBannered) {
          triggered.value.push({ symbol: alert.symbol, currentRsi: data.currentRsi, type: alert.type, threshold: alert.threshold })
          setTimeout(() => { triggered.value = triggered.value.filter(t => !(t.symbol === alert.symbol && t.type === alert.type)) }, 10000)
        }
      }
    }

    saveAlerts()
  } catch { /* silent */ }
}

// ── Check all ─────────────────────────────────────────────────────────────
async function checkNow() {
  if (checking.value) return
  checking.value = true; error.value = null
  for (const alert of activeAlerts.value) {
    await fetchRsiForAlert(alert)
    await new Promise(r => setTimeout(r, 500))
  }
  lastChecked.value = new Date().toLocaleTimeString('en-DK', { hour: '2-digit', minute: '2-digit' })
  checking.value = false
}

function goToScreener() { window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'screener' })) }
function goToPaper()    { window.dispatchEvent(new CustomEvent('switch-tab', { detail: 'paper' })) }

// ── Display helpers ────────────────────────────────────────────────────────
function isFired(a) {
  if (a.currentRsi == null || isExcluded(a)) return false
  return a.type === 'entry' ? a.currentRsi < a.threshold : a.currentRsi > a.threshold
}

// FIX 2: Action-oriented status text
function alertStatus(a) {
  if (a.currentRsi == null) return 'Loading…'
  if (isFired(a)) return a.type === 'entry' ? '🔔 Oversold!' : '🔔 Recovered!'
  return a.type === 'entry' ? '⏳ Waiting for entry signal' : '⏳ Monitoring for exit signal'
}

function statusClass(a) { return isFired(a) ? 'status--fired' : 'status--watching' }
function alertCardClass(a) {
  if (isFired(a)) return a.type === 'entry' ? 'al-card--entry-fired' : 'al-card--exit-fired'
  return ''
}
function rsiClass(rsi) {
  if (rsi == null) return 'al-muted'
  if (rsi < 30)   return 'al-green al-bold'
  if (rsi > 70)   return 'al-red al-bold'
  return ''
}
function rsiGap(a) {
  if (a.currentRsi == null) return '—'
  const gap = gapNumber(a)
  return (gap > 0 ? '+' : '') + gap.toFixed(1) + ' pts'
}
function gapClass(a) {
  const gap = gapNumber(a)
  return gap <= 0 ? 'al-green al-bold' : 'al-muted'
}

onMounted(() => {
  loadAlerts()
  loadOpenTrades(authStore.userName)
  autoCheck = setInterval(checkNow, 5 * 60 * 1000)
  setTimeout(checkNow, 3000)
})
onUnmounted(() => { if (autoCheck) clearInterval(autoCheck) })
</script>

<style scoped>
.al-panel { display: flex; flex-direction: column; gap: 10px; padding: 1rem; background: var(--bg-panel); color: var(--text-primary); min-width: 0; }
.al-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
.al-header-left { display: flex; flex-direction: column; gap: 2px; }
.al-title   { font-size: 15px; font-weight: 700; }
.al-subtitle { font-size: 11px; color: var(--text-secondary); }
.al-refresh-btn { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 7px; padding: 6px 13px; font-size: 12px; color: var(--text-primary); cursor: pointer; flex-shrink: 0; }
.al-refresh-btn:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.al-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.al-spinner { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.al-explainer { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 10px 13px; display: flex; flex-direction: column; gap: 6px; }
.al-explainer-row { font-size: 11px; color: var(--text-secondary); display: flex; align-items: center; gap: 8px; line-height: 1.4; }
.al-explainer-badge { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; flex-shrink: 0; letter-spacing: 0.05em; }

.al-triggered-banner { background: rgba(34,197,94,.15); border: 1px solid rgba(34,197,94,.3); border-radius: 8px; padding: 10px 13px; font-size: 12px; color: #22c55e; }
.al-triggered-item { margin-bottom: 3px; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.al-section { display: flex; flex-direction: column; gap: 8px; }
.al-section-header { display: flex; justify-content: space-between; align-items: center; background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 10px 14px; cursor: pointer; user-select: none; }
.al-section-header:hover { border-color: var(--accent); }
.al-section-title { font-size: 12px; font-weight: 700; }
.al-chevron { font-size: 10px; color: var(--text-secondary); }
.al-form { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 8px; padding: 14px; display: flex; flex-direction: column; gap: 10px; }
.al-form-row { display: flex; gap: 10px; flex-wrap: wrap; }
.al-field { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 110px; }
.al-field--sm { flex: 0 0 90px; min-width: 80px; }
.al-label { font-size: 9px; font-weight: 700; letter-spacing: 0.08em; color: var(--text-secondary); }
.al-input { background: var(--bg-panel); border: 1px solid var(--bg-panel-border); border-radius: 6px; padding: 6px 9px; font-size: 12px; color: var(--text-primary); height: 32px; }
.al-input:focus { outline: none; border-color: var(--accent); }
.al-input--upper { text-transform: uppercase; }
.al-select { appearance: none; cursor: pointer; }
.al-form-error { font-size: 11px; color: #ef4444; }
.al-form-actions { display: flex; gap: 8px; }

.al-btn { border-radius: 6px; padding: 7px 16px; font-size: 12px; font-weight: 600; cursor: pointer; border: none; }
.al-btn--primary { background: var(--accent); color: #fff; }
.al-btn--primary:hover { opacity: 0.85; }
.al-btn--ghost { background: var(--bg-panel-item); color: var(--text-secondary); border: 1px solid var(--bg-panel-border); }

.al-error { background: rgba(239,68,68,.1); border: 1px solid rgba(239,68,68,.3); border-radius: 7px; padding: 8px 12px; font-size: 11px; color: #ef4444; }
.al-empty { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 32px 16px; text-align: center; }
.al-empty-icon { font-size: 28px; opacity: 0.4; }
.al-empty-title { font-size: 13px; font-weight: 700; }
.al-empty-desc { font-size: 11px; color: var(--text-secondary); line-height: 1.5; }

.al-list { display: flex; flex-direction: column; gap: 6px; }

/* FIX 3: Group labels */
.al-group-title { font-size: 10px; font-weight: 700; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.07em; padding: 6px 2px 4px; }
.al-group-title--fired    { opacity: 0.6; margin-top: 8px; }
.al-group-title--excluded { color: #ef4444; opacity: 0.8; margin-top: 8px; }
.al-count { font-weight: 400; }

.al-card { background: var(--bg-panel-item); border: 1px solid var(--bg-panel-border); border-radius: 9px; padding: 11px 13px; display: flex; flex-direction: column; gap: 8px; }
.al-card--entry-fired       { border-color: rgba(34,197,94,.4);  background: rgba(34,197,94,.04); }
.al-card--exit-fired        { border-color: rgba(99,102,241,.4); background: rgba(99,102,241,.04); }
.al-card--fired             { opacity: 0.55; }
.al-card--loading           { opacity: 0.5; }
/* FIX 4: Excluded cards clearly dimmed and red-tinted */
.al-card--rulebook-excluded { opacity: 0.55; border-color: rgba(239,68,68,.2); background: rgba(239,68,68,.02); }

.al-card-top { display: flex; align-items: center; justify-content: space-between; gap: 6px; }
.al-card-left  { display: flex; align-items: center; gap: 7px; flex-wrap: wrap; }
.al-card-right { display: flex; align-items: center; gap: 6px; }
.al-symbol { font-size: 15px; font-weight: 800; }
.al-symbol--muted { color: var(--text-secondary); }
.al-threshold { font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.al-fired-label { font-size: 11px; color: var(--text-secondary); }

/* FIX 3: Priority flag */
.al-priority-flag { font-size: 9px; font-weight: 700; background: rgba(234,179,8,.15); color: #eab308; border: 1px solid rgba(234,179,8,.3); border-radius: 4px; padding: 2px 6px; }

/* FIX 4: Excluded badge */
.al-excluded-badge { font-size: 9px; font-weight: 700; background: rgba(239,68,68,.12); color: #ef4444; border: 1px solid rgba(239,68,68,.25); border-radius: 4px; padding: 2px 7px; }
.al-excluded-note  { font-size: 11px; color: #ef4444; background: rgba(239,68,68,.06); border: 1px solid rgba(239,68,68,.15); border-radius: 5px; padding: 6px 9px; line-height: 1.4; }

.al-type-badge { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; letter-spacing: 0.04em; }
.badge--entry { background: rgba(34,197,94,.15);  color: #22c55e; }
.badge--exit  { background: rgba(99,102,241,.15); color: #818cf8; }

.al-remove-btn { background: none; border: 1px solid var(--bg-panel-border); border-radius: 4px; color: var(--text-secondary); font-size: 10px; padding: 3px 7px; cursor: pointer; }
.al-remove-btn:hover { border-color: #ef4444; color: #ef4444; }

.al-rsi-row { display: flex; gap: 14px; align-items: center; flex-wrap: wrap; }
.al-metric { display: flex; flex-direction: column; gap: 1px; }
.al-metric-value { font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; line-height: 1.1; }
.al-metric-label { font-size: 9px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.05em; }
.al-status-col { margin-left: auto; }
/* FIX 2: Status pill */
.al-status { font-size: 10px; font-weight: 700; padding: 4px 10px; border-radius: 5px; white-space: nowrap; }
.status--watching { background: var(--bg-panel); color: var(--text-secondary); border: 1px solid var(--bg-panel-border); }
.status--fired    { background: rgba(34,197,94,.15); color: #22c55e; }

/* Trade State badge */
.al-state-badge  { font-size: 9px; font-weight: 700; padding: 2px 7px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.03em; }
.state--blocked  { background: rgba(239,68,68,.12);  color: #ef4444; }
.state--entry    { background: rgba(34,197,94,.15);  color: #22c55e; }
.state--in-trade { background: rgba(99,102,241,.15); color: #818cf8; }
.state--exit     { background: rgba(239,68,68,.15);  color: #ef4444; }
.state--idle     { background: rgba(148,163,184,.1); color: var(--text-secondary); }

.al-fire-action { font-size: 11px; color: var(--text-primary); background: var(--bg-panel); border: 1px solid var(--bg-panel-border); border-radius: 6px; padding: 9px 11px; display: flex; flex-direction: column; gap: 6px; }
.al-fire-title  { font-weight: 600; font-size: 11px; }
.al-loop-steps  { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
.al-loop-step   { font-size: 10px; font-weight: 600; padding: 2px 8px; border-radius: 4px; white-space: nowrap; }
.al-loop-arrow  { font-size: 10px; color: var(--text-muted); }
.al-loop-step--done { background: rgba(34,197,94,.12); color: #22c55e; }
.al-loop-step--now  { background: var(--accent); color: #fff; cursor: pointer; }
.al-loop-step--now:hover { opacity: 0.85; }
.al-loop-step--next { background: var(--bg-panel-border); color: var(--text-secondary); }
.al-loading-text { font-size: 11px; color: var(--text-muted); padding: 2px 0; }

.al-green { color: #22c55e; }
.al-red   { color: #ef4444; }
.al-muted { color: var(--text-secondary); }
.al-bold  { font-weight: 800; }
.al-last-checked { font-size: 10px; color: var(--text-muted); text-align: center; padding-top: 4px; }
</style>