<template>
  <div class="pp-panel">

    <!-- Header -->
    <div class="pp-header">
      <div class="pp-header-left">
        <span class="pp-title">📋 Paper Portfolio</span>
        <span class="pp-subtitle">Track RSI screener picks · Benchmark: Buy &amp; Hold from entry date</span>
      </div>
      <button class="pp-refresh-btn" :disabled="loading" @click="loadPortfolio">
        <span v-if="loading" class="pp-spinner">⟳</span>
        <span v-else>⟳ Refresh</span>
      </button>
    </div>

    <!-- Error -->
    <div v-if="error" class="pp-error">⚠️ {{ error }}</div>

    <!-- Summary stats (only when there's data) -->
    <div v-if="summary && (summary.openCount > 0 || summary.closedCount > 0)" class="pp-stats-row">
      <div class="pp-stat-card">
        <div class="pp-stat-value pp-accent">{{ summary.openCount }}</div>
        <div class="pp-stat-label">Open Trades</div>
      </div>
      <div class="pp-stat-card">
        <div class="pp-stat-value pp-muted">{{ summary.closedCount }}</div>
        <div class="pp-stat-label">Closed</div>
      </div>
      <!-- FIX #2 — "Win Rate" → "Trade Win Rate" -->
      <div class="pp-stat-card" v-if="summary.closedCount > 0">
        <div class="pp-stat-value" :class="summary.winRate >= 50 ? 'pp-green' : 'pp-red'">
          {{ summary.winRate ?? '—' }}%
        </div>
        <div class="pp-stat-label">Trade Win Rate</div>
      </div>
      <div class="pp-stat-card" v-if="summary.closedCount > 0">
        <div class="pp-stat-value" :class="(summary.beatBenchmarkRate ?? 0) >= 50 ? 'pp-green' : 'pp-red'">
          {{ summary.beatBenchmarkRate ?? '—' }}%
        </div>
        <div class="pp-stat-label">Beat B&amp;H</div>
      </div>
      <div class="pp-stat-card" v-if="summary.closedCount > 0">
        <div class="pp-stat-value" :class="(summary.avgTradeReturn ?? 0) >= 0 ? 'pp-green' : 'pp-red'">
          {{ summary.avgTradeReturn != null ? summary.avgTradeReturn + '%' : '—' }}
        </div>
        <div class="pp-stat-label">Avg Return</div>
      </div>
      <div class="pp-stat-card" v-if="summary.closedCount > 0">
        <div class="pp-stat-value" :class="(summary.avgVsBenchmark ?? 0) >= 0 ? 'pp-green' : 'pp-red'">
          {{ summary.avgVsBenchmark != null ? (summary.avgVsBenchmark > 0 ? '+' : '') + summary.avgVsBenchmark + '%' : '—' }}
        </div>
        <div class="pp-stat-label">Avg vs B&amp;H</div>
      </div>
    </div>

    <!-- FIX #3 — Limited evidence warning when < 5 closed trades -->
    <div v-if="summary && summary.closedCount > 0 && summary.closedCount < 5" class="pp-evidence-warning">
      <span class="pp-evidence-icon">⚠️</span>
      <span class="pp-evidence-text">
        <strong>Limited evidence:</strong> Results based on {{ summary.closedCount }} completed trade{{ summary.closedCount === 1 ? '' : 's' }}.
        Meaningful conclusions require 20–30 closed trades.
      </span>
    </div>

    <!-- Open new trade form -->
    <div class="pp-section">
      <div class="pp-section-header" @click="showForm = !showForm">
        <span class="pp-section-title">➕ Open New Paper Trade</span>
        <span class="pp-chevron">{{ showForm ? '▲' : '▼' }}</span>
      </div>
      <div v-if="showForm" class="pp-form">
        <div class="pp-form-row">
          <div class="pp-field">
            <label class="pp-label">SYMBOL</label>
            <input class="pp-input pp-input--upper" v-model="form.symbol" placeholder="e.g. SLB" maxlength="6" />
          </div>
          <div class="pp-field">
            <label class="pp-label">SECTOR</label>
            <input class="pp-input" v-model="form.sector" placeholder="e.g. energy" />
          </div>
          <div class="pp-field">
            <label class="pp-label">ENTRY PRICE ($)</label>
            <input class="pp-input" v-model="form.entryPrice" type="number" step="0.01" placeholder="0.00" />
          </div>
          <div class="pp-field">
            <label class="pp-label">ENTRY DATE</label>
            <input class="pp-input" v-model="form.entryDate" type="date" />
          </div>
        </div>
        <div class="pp-form-row">
          <div class="pp-field">
            <label class="pp-label">RSI AT ENTRY</label>
            <input class="pp-input" v-model="form.rsiAtEntry" type="number" step="0.1" placeholder="e.g. 28.4" />
          </div>
          <div class="pp-field">
            <label class="pp-label">TARGET EXIT RSI</label>
            <input class="pp-input" v-model="form.targetExitRsi" type="number" step="0.1" placeholder="e.g. 60" />
          </div>
        </div>
        <div class="pp-field pp-field--full">
          <label class="pp-label">WHY THIS TRADE? <span class="pp-label-hint">(optional — one sentence)</span></label>
          <textarea
            class="pp-textarea"
            v-model="form.notes"
            placeholder="e.g. SLB oversold in energy sector, RSI 28, aligns with Finding #3 — no strong trend exclusion."
            rows="2"
            maxlength="300"
          ></textarea>
        </div>
        <div v-if="formError" class="pp-form-error">⚠️ {{ formError }}</div>
        <div class="pp-form-actions">
          <button class="pp-btn pp-btn--primary" :disabled="formLoading" @click="openTrade">
            {{ formLoading ? 'Opening…' : '✓ Open Trade' }}
          </button>
          <button class="pp-btn pp-btn--ghost" @click="resetForm">Cancel</button>
        </div>
      </div>
    </div>

    <!-- Open positions -->
    <div class="pp-section" v-if="summary && summary.openTrades.length > 0">
      <div class="pp-section-title pp-section-title--plain">
        📂 Open Positions <span class="pp-count">({{ summary.openTrades.length }})</span>
      </div>
      <div class="pp-table-wrap">
        <table class="pp-table">
          <thead>
            <tr>
              <th class="pp-th">Symbol</th>
              <th class="pp-th">Sector</th>
              <th class="pp-th pp-th-num">Entry Price</th>
              <th class="pp-th">Entry Date</th>
              <th class="pp-th pp-th-num">RSI Entry</th>
              <th class="pp-th pp-th-num">Target RSI</th>
              <th class="pp-th pp-th-num">Days Held</th>
              <th class="pp-th pp-th-num">Unrealized P&amp;L</th>
              <th class="pp-th pp-th-center">Actions</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="t in summary.openTrades" :key="t.id">
              <tr class="pp-row">
                <td class="pp-td pp-td-symbol">
                  {{ t.symbol }}
                  <div v-if="t.notes" class="pp-note">{{ t.notes }}</div>
                </td>
                <td class="pp-td pp-td-sector">{{ t.sector }}</td>
                <td class="pp-td pp-td-num">${{ t.entryPrice }}</td>
                <td class="pp-td pp-td-date">{{ formatDate(t.entryDate) }}</td>
                <td class="pp-td pp-td-num pp-rsi-entry">{{ t.rsiAtEntry }}</td>
                <td class="pp-td pp-td-num pp-muted">{{ t.targetExitRsi }}</td>
                <td class="pp-td pp-td-num pp-muted">{{ t.daysHeld }}d</td>
                <td class="pp-td pp-td-num">
                  <span v-if="t.unrealizedPct != null"
                    :class="t.unrealizedPct >= 0 ? 'pp-green' : 'pp-red'">
                    {{ t.unrealizedPct > 0 ? '+' : '' }}{{ t.unrealizedPct }}%
                  </span>
                  <span v-else class="pp-muted">—</span>
                </td>
                <td class="pp-td pp-td-center">
                  <button class="pp-action-btn pp-action-btn--close"
                    @click="startClose(t)">Close</button>
                  <button class="pp-action-btn pp-action-btn--delete"
                    @click="deleteTrade(t.id)">✕</button>
                </td>
              </tr>
              <!-- Inline close form -->
              <tr v-if="closingId === t.id" class="pp-close-row">
                <td colspan="9">
                  <div class="pp-close-form">
                    <div class="pp-close-form-title">Close {{ t.symbol }} position</div>
                    <div class="pp-form-row">
                      <div class="pp-field">
                        <label class="pp-label">EXIT PRICE ($)</label>
                        <input class="pp-input" v-model="closeForm.exitPrice" type="number" step="0.01" />
                      </div>
                      <div class="pp-field">
                        <label class="pp-label">EXIT DATE</label>
                        <input class="pp-input" v-model="closeForm.exitDate" type="date" />
                      </div>
                      <div class="pp-field">
                        <label class="pp-label">RSI AT EXIT</label>
                        <input class="pp-input" v-model="closeForm.rsiAtExit" type="number" step="0.1" />
                      </div>
                    </div>
                    <div v-if="closeError" class="pp-form-error">⚠️ {{ closeError }}</div>
                    <div class="pp-form-actions">
                      <button class="pp-btn pp-btn--primary" :disabled="closeLoading" @click="closeTrade(t.id)">
                        {{ closeLoading ? 'Closing…' : '✓ Confirm Close' }}
                      </button>
                      <button class="pp-btn pp-btn--ghost" @click="closingId = null">Cancel</button>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Closed trades -->
    <div class="pp-section" v-if="summary && summary.closedTrades.length > 0">
      <div class="pp-section-title pp-section-title--plain">
        📊 Closed Trades <span class="pp-count">({{ summary.closedTrades.length }})</span>
      </div>
      <div class="pp-table-wrap">
        <table class="pp-table">
          <thead>
            <tr>
              <th class="pp-th">Symbol</th>
              <th class="pp-th">Sector</th>
              <th class="pp-th pp-th-num">Entry</th>
              <th class="pp-th pp-th-num">Exit</th>
              <th class="pp-th pp-th-num">RSI In→Out</th>
              <th class="pp-th pp-th-num">Days</th>
              <th class="pp-th pp-th-num">Trade Return</th>
              <th class="pp-th pp-th-num">B&amp;H Return</th>
              <th class="pp-th pp-th-num">vs B&amp;H</th>
              <th class="pp-th pp-th-center">Result</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in summary.closedTrades" :key="t.id" class="pp-row"
              :class="t.beatBenchmark ? 'pp-row--win' : 'pp-row--loss'">
              <td class="pp-td pp-td-symbol">
                {{ t.symbol }}
                <div v-if="t.notes" class="pp-note">{{ t.notes }}</div>
              </td>
              <td class="pp-td pp-td-sector">{{ t.sector }}</td>
              <td class="pp-td pp-td-num">
                <div>${{ t.entryPrice }}</div>
                <div class="pp-date-sub">{{ formatDate(t.entryDate) }}</div>
              </td>
              <td class="pp-td pp-td-num">
                <div>${{ t.exitPrice }}</div>
                <div class="pp-date-sub">{{ formatDate(t.exitDate) }}</div>
              </td>
              <td class="pp-td pp-td-num pp-rsi-range">
                {{ t.rsiAtEntry }} → {{ t.rsiAtExit ?? '—' }}
              </td>
              <td class="pp-td pp-td-num pp-muted">{{ t.daysHeld }}d</td>
              <td class="pp-td pp-td-num">
                <span v-if="t.tradePct != null" :class="t.tradePct >= 0 ? 'pp-green' : 'pp-red'">
                  {{ t.tradePct > 0 ? '+' : '' }}{{ t.tradePct }}%
                </span>
                <span v-else class="pp-muted">—</span>
              </td>
              <!-- FIX #1 — B&H Return: show value or explain why missing -->
              <td class="pp-td pp-td-num">
                <span v-if="t.benchmarkBahReturn != null" class="pp-muted">
                  {{ t.benchmarkBahReturn > 0 ? '+' : '' }}{{ t.benchmarkBahReturn }}%
                </span>
                <span v-else class="pp-bah-pending" title="B&H data pending — will populate when price history is available">
                  Pending
                </span>
              </td>
              <td class="pp-td pp-td-num">
                <span v-if="t.vsBenchmark != null"
                  :class="t.vsBenchmark >= 0 ? 'pp-green pp-bold' : 'pp-red pp-bold'">
                  {{ t.vsBenchmark > 0 ? '+' : '' }}{{ t.vsBenchmark }}pp
                </span>
                <span v-else class="pp-muted">—</span>
              </td>
              <td class="pp-td pp-td-center">
                <!-- FIX #1 — Only show result badge when B&H data exists -->
                <span v-if="t.benchmarkBahReturn != null"
                  class="pp-result-badge" :class="t.beatBenchmark ? 'badge--beat' : 'badge--lost'">
                  {{ t.beatBenchmark ? '✅ Beat B&H' : '❌ Lost to B&H' }}
                </span>
                <span v-else class="pp-result-badge badge--pending">
                  ⏳ Awaiting B&H
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Empty state -->
    <div v-if="!loading && summary && summary.openCount === 0 && summary.closedCount === 0"
      class="pp-empty-state">
      <div class="pp-empty-icon">📋</div>
      <div class="pp-empty-title">No paper trades yet</div>
      <div class="pp-empty-desc">
        Open a trade above to start tracking RSI screener picks against the Buy &amp; Hold benchmark.
        When you close a trade, the B&amp;H return for the same period is fetched automatically from Yahoo Finance.
      </div>
    </div>

    <!-- Initial loading -->
    <div v-if="!loading && !summary && !error" class="pp-empty-state">
      <div class="pp-empty-icon">📋</div>
      <div class="pp-empty-title">Paper Portfolio</div>
      <div class="pp-empty-desc">Loading your trades…</div>
    </div>

  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'

const BASE = 'http://localhost:60363'

const userName = () => localStorage.getItem('userName') ?? 'test2'

const summary      = ref(null)
const loading      = ref(false)
const error        = ref(null)
const showForm     = ref(false)
const closingId    = ref(null)
const formLoading  = ref(false)
const closeLoading = ref(false)
const formError    = ref(null)
const closeError   = ref(null)

const form = ref({
  symbol: '', sector: '', entryPrice: '', entryDate: today(),
  rsiAtEntry: '', targetExitRsi: '60', notes: ''
})

const closeForm = ref({
  exitPrice: '', exitDate: today(), rsiAtExit: ''
})

function today() {
  return new Date().toISOString().slice(0, 10)
}

function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-DK', { day: '2-digit', month: 'short', year: '2-digit' })
}

async function loadPortfolio() {
  loading.value = true
  error.value   = null
  try {
    const res = await fetch(`${BASE}/api/paper/${userName()}`)
    const rawText = await res.text()
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${rawText.slice(0, 200)}`)
    summary.value = JSON.parse(rawText)
  } catch (e) {
    error.value = 'Failed to load portfolio: ' + e.message
  } finally {
    loading.value = false
  }
}

async function openTrade() {
  formError.value = null
  if (!form.value.symbol) return (formError.value = 'Symbol is required.')
  if (!form.value.entryPrice || Number(form.value.entryPrice) <= 0)
    return (formError.value = 'Entry price must be greater than 0.')
  if (!form.value.rsiAtEntry)
    return (formError.value = 'RSI at entry is required.')

  formLoading.value = true
  try {
    const res = await fetch(`${BASE}/api/paper/open`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        userName:      userName(),
        symbol:        form.value.symbol.toUpperCase(),
        sector:        form.value.sector.toLowerCase(),
        entryPrice:    parseFloat(form.value.entryPrice),
        entryDate:     form.value.entryDate,
        rsiAtEntry:    parseFloat(form.value.rsiAtEntry),
        targetExitRsi: parseFloat(form.value.targetExitRsi),
        notes:         form.value.notes.trim() || null
      })
    })
    if (!res.ok) {
      const body = await res.json()
      throw new Error(body.error ?? `HTTP ${res.status}`)
    }
    resetForm()
    await loadPortfolio()
  } catch (e) {
    formError.value = e.message
  } finally {
    formLoading.value = false
  }
}

function startClose(trade) {
  closingId.value = trade.id
  closeError.value = null
  closeForm.value = { exitPrice: '', exitDate: today(), rsiAtExit: '' }
}

async function closeTrade(tradeId) {
  closeError.value = null
  if (!closeForm.value.exitPrice || Number(closeForm.value.exitPrice) <= 0)
    return (closeError.value = 'Exit price must be greater than 0.')
  if (!closeForm.value.rsiAtExit)
    return (closeError.value = 'RSI at exit is required.')

  closeLoading.value = true
  try {
    const res = await fetch(`${BASE}/api/paper/close`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tradeId:   tradeId,
        userName:  userName(),
        exitPrice: Number(closeForm.value.exitPrice),
        exitDate:  closeForm.value.exitDate,
        rsiAtExit: Number(closeForm.value.rsiAtExit)
      })
    })
    if (!res.ok) {
      const body = await res.json()
      throw new Error(body.error ?? `HTTP ${res.status}`)
    }
    closingId.value = null
    await loadPortfolio()
  } catch (e) {
    closeError.value = e.message
  } finally {
    closeLoading.value = false
  }
}

async function deleteTrade(tradeId) {
  if (!confirm('Delete this open trade?')) return
  try {
    const res = await fetch(`${BASE}/api/paper/${tradeId}/${userName()}`, { method: 'DELETE' })
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    await loadPortfolio()
  } catch (e) {
    error.value = 'Delete failed: ' + e.message
  }
}

function resetForm() {
  showForm.value  = false
  formError.value = null
  form.value = { symbol: '', sector: '', entryPrice: '', entryDate: today(), rsiAtEntry: '', targetExitRsi: '60', notes: '' }
}

onMounted(loadPortfolio)
</script>

<style scoped>
.pp-panel {
  --pp-bg:     var(--bg-panel);
  --pp-bg2:    var(--bg-panel-item);
  --pp-border: var(--bg-panel-border);
  --pp-text:   var(--text-primary);
  --pp-muted:  var(--text-secondary);
  --pp-accent: var(--accent);
  --pp-green:  #22c55e;
  --pp-red:    #ef4444;
  --pp-yellow: #eab308;

  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 1rem;
  min-width: 0;
  background: var(--pp-bg);
  color: var(--pp-text);
}

/* Header */
.pp-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.pp-header-left { display: flex; flex-direction: column; gap: 3px; }
.pp-title    { font-size: 15px; font-weight: 700; }
.pp-subtitle { font-size: 11px; color: var(--pp-muted); }

.pp-refresh-btn {
  background: var(--pp-bg2);
  border: 1px solid var(--pp-border);
  border-radius: 7px;
  padding: 7px 14px;
  font-size: 12px;
  color: var(--pp-text);
  cursor: pointer;
  flex-shrink: 0;
}
.pp-refresh-btn:hover:not(:disabled) { border-color: var(--pp-accent); color: var(--pp-accent); }
.pp-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }

/* Error */
.pp-error {
  background: rgba(239,68,68,.1);
  border: 1px solid rgba(239,68,68,.3);
  border-radius: 7px;
  padding: 10px 14px;
  font-size: 12px;
  color: var(--pp-red);
}

/* FIX #3 — Evidence warning */
.pp-evidence-warning {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  background: rgba(234,179,8,.07);
  border: 1px solid rgba(234,179,8,.25);
  border-radius: 7px;
  padding: 9px 13px;
  font-size: 11px;
  line-height: 1.5;
}
.pp-evidence-icon { flex-shrink: 0; }
.pp-evidence-text { color: var(--pp-muted); }
.pp-evidence-text strong { color: var(--pp-text); }

/* Stats */
.pp-stats-row { display: flex; gap: 8px; flex-wrap: wrap; }
.pp-stat-card {
  background: var(--pp-bg2);
  border: 1px solid var(--pp-border);
  border-radius: 8px;
  padding: 8px 14px;
  text-align: center;
  min-width: 80px;
  flex: 1;
}
.pp-stat-value { font-size: 20px; font-weight: 700; font-variant-numeric: tabular-nums; }
.pp-stat-label { font-size: 9px; color: var(--pp-muted); text-transform: uppercase; letter-spacing: 0.06em; margin-top: 2px; }

/* Colour helpers */
.pp-green  { color: var(--pp-green); }
.pp-red    { color: var(--pp-red); }
.pp-muted  { color: var(--pp-muted); }
.pp-accent { color: var(--pp-accent); }
.pp-bold   { font-weight: 700; }

/* Section */
.pp-section { display: flex; flex-direction: column; gap: 8px; }
.pp-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--pp-bg2);
  border: 1px solid var(--pp-border);
  border-radius: 8px;
  padding: 10px 14px;
  cursor: pointer;
  user-select: none;
}
.pp-section-header:hover { border-color: var(--pp-accent); }
.pp-section-title { font-size: 12px; font-weight: 700; }
.pp-section-title--plain { font-size: 12px; font-weight: 700; padding: 4px 0; }
.pp-chevron { font-size: 10px; color: var(--pp-muted); }
.pp-count   { font-size: 11px; font-weight: 400; color: var(--pp-muted); margin-left: 4px; }

/* Form */
.pp-form {
  background: var(--pp-bg2);
  border: 1px solid var(--pp-border);
  border-radius: 8px;
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.pp-form-row { display: flex; gap: 10px; flex-wrap: wrap; }
.pp-field { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 120px; }
.pp-field--full { flex: 1 1 100%; min-width: 0; }
.pp-label { font-size: 9px; font-weight: 700; letter-spacing: 0.08em; color: var(--pp-muted); }
.pp-label-hint { font-weight: 400; text-transform: none; letter-spacing: 0; opacity: 0.7; }
.pp-input {
  background: var(--pp-bg);
  border: 1px solid var(--pp-border);
  border-radius: 6px;
  padding: 6px 9px;
  font-size: 12px;
  color: var(--pp-text);
  height: 32px;
}
.pp-input:focus { outline: none; border-color: var(--pp-accent); }
.pp-input--upper { text-transform: uppercase; }

.pp-textarea {
  background: var(--pp-bg);
  border: 1px solid var(--pp-border);
  border-radius: 6px;
  padding: 7px 9px;
  font-size: 12px;
  color: var(--pp-text);
  font-family: inherit;
  resize: vertical;
  line-height: 1.5;
}
.pp-textarea:focus { outline: none; border-color: var(--pp-accent); }

.pp-note {
  font-size: 10px;
  color: var(--pp-muted);
  font-weight: 400;
  margin-top: 3px;
  line-height: 1.4;
  max-width: 260px;
  white-space: normal;
  font-style: italic;
}

.pp-form-error {
  font-size: 11px;
  color: var(--pp-red);
  padding: 4px 0;
}
.pp-form-actions { display: flex; gap: 8px; }

/* Close form (inline) */
.pp-close-row { background: rgba(var(--pp-accent), 0.03); }
.pp-close-form {
  padding: 12px 14px;
  background: var(--pp-bg);
  border: 1px solid var(--pp-border);
  border-radius: 8px;
  margin: 4px 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.pp-close-form-title { font-size: 12px; font-weight: 700; color: var(--pp-accent); }

/* Buttons */
.pp-btn {
  border-radius: 6px;
  padding: 7px 16px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  border: none;
  transition: opacity 0.15s;
}
.pp-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.pp-btn--primary { background: var(--pp-accent); color: #fff; }
.pp-btn--primary:hover:not(:disabled) { opacity: 0.85; }
.pp-btn--ghost {
  background: var(--pp-bg2);
  color: var(--pp-muted);
  border: 1px solid var(--pp-border);
}
.pp-btn--ghost:hover:not(:disabled) { color: var(--pp-text); }

/* Tables */
.pp-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--pp-border);
  border-radius: 8px;
}
.pp-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.pp-th {
  background: var(--pp-bg2);
  padding: 8px 11px;
  text-align: left;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.07em;
  color: var(--pp-muted);
  border-bottom: 1px solid var(--pp-border);
  white-space: nowrap;
}
.pp-th-num    { text-align: right; }
.pp-th-center { text-align: center; }

.pp-td {
  padding: 9px 11px;
  border-bottom: 1px solid var(--pp-border);
  vertical-align: middle;
}
.pp-td-num    { text-align: right; font-variant-numeric: tabular-nums; }
.pp-td-center { text-align: center; }
.pp-td-symbol { font-weight: 700; font-size: 13px; }
.pp-td-sector { color: var(--pp-muted); font-size: 11px; text-transform: capitalize; }
.pp-td-date   { font-size: 11px; color: var(--pp-muted); white-space: nowrap; }
.pp-date-sub  { font-size: 10px; color: var(--pp-muted); margin-top: 1px; }
.pp-rsi-entry { color: var(--pp-green); font-weight: 700; }
.pp-rsi-range { color: var(--pp-muted); }

.pp-row:hover  { background: var(--pp-bg2); }
.pp-row--win   { background: rgba(34,197,94,.03); }
.pp-row--loss  { background: rgba(239,68,68,.02); }
.pp-spinner    { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* FIX #1 — B&H pending state */
.pp-bah-pending {
  font-size: 10px;
  color: var(--pp-muted);
  font-style: italic;
}

/* Action buttons */
.pp-action-btn {
  font-size: 10px;
  font-weight: 600;
  padding: 3px 9px;
  border-radius: 4px;
  cursor: pointer;
  border: 1px solid var(--pp-border);
  background: none;
  margin-left: 4px;
}
.pp-action-btn--close { color: var(--pp-accent); border-color: var(--pp-accent); }
.pp-action-btn--close:hover { background: rgba(99,102,241,.1); }
.pp-action-btn--delete { color: var(--pp-red); border-color: rgba(239,68,68,.3); }
.pp-action-btn--delete:hover { background: rgba(239,68,68,.1); }

/* Result badge */
.pp-result-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 4px;
  white-space: nowrap;
}
.badge--beat    { background: rgba(34,197,94,.15);  color: var(--pp-green); }
.badge--lost    { background: rgba(239,68,68,.12);  color: var(--pp-red); }
.badge--pending { background: var(--pp-bg2); color: var(--pp-muted); font-weight: 400; }

/* Empty state */
.pp-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  padding: 48px 24px;
  text-align: center;
}
.pp-empty-icon  { font-size: 36px; opacity: 0.4; }
.pp-empty-title { font-size: 14px; font-weight: 700; }
.pp-empty-desc  { font-size: 12px; color: var(--pp-muted); line-height: 1.6; max-width: 440px; }
</style>