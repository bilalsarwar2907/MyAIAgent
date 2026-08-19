// useTradeState.js
// ─────────────────────────────────────────────────────────────────────────────
// Single canonical trade state engine.
// All tabs (Watchlist, Alerts, Portfolio) import this and call getState().
//
// State machine:
//   ❌ Blocked    — trendBucket Strong (>300%) → Finding #1 violation
//   🔵 In Trade   — symbol has an open paper trade
//   🔴 Exit Signal — In Trade + RSI > exitThreshold (default 60)
//   🟢 Entry Ready — RSI < entryThreshold (default 30) + not blocked + not in trade
//   ⚪ Idle        — everything else (waiting, neutral, unknown)
//
// Usage:
//   import { useTradeState } from '@/composables/useTradeState'
//   const { openSymbols, loadOpenTrades, getState, stateLabel, stateClass } = useTradeState()
//   await loadOpenTrades()
//   const state = getState(item)  // { id, label, icon, class }

import { ref } from 'vue'

const BASE = import.meta.env?.VITE_API_BASE_URL || 'http://localhost:60363'

// Shared reactive set of symbols with open paper trades.
// Shared across all tabs so only one fetch is needed per session.
const openSymbols   = ref(new Set())
const lastFetchedAt = ref(null)
let   fetchPromise  = null   // deduplicates concurrent calls

export function useTradeState() {

  // ── Load open trades from Paper Portfolio API ────────────────────────────
  async function loadOpenTrades(userName) {
    // Deduplicate: if already fetching, wait for the same promise
    if (fetchPromise) return fetchPromise

    // Cache: don't re-fetch if loaded in the last 2 minutes
    if (lastFetchedAt.value && (Date.now() - lastFetchedAt.value) < 2 * 60 * 1000) return

    fetchPromise = (async () => {
      try {
        const user = userName || localStorage.getItem('userName') || 'test2'
        const res  = await fetch(`${BASE}/api/paper/${user}`)
        if (!res.ok) return
        const data = await res.json()
        const symbols = (data.openTrades ?? []).map(t => t.symbol.toUpperCase())
        openSymbols.value   = new Set(symbols)
        lastFetchedAt.value = Date.now()
      } catch {
        /* silent — state engine degrades gracefully, just won't show In Trade */
      } finally {
        fetchPromise = null
      }
    })()

    return fetchPromise
  }

  // ── Force refresh (call after opening or closing a trade) ────────────────
  function invalidateCache() {
    lastFetchedAt.value = null
    fetchPromise        = null
  }

  // ── Core state function ───────────────────────────────────────────────────
  // item must have: { symbol, trendBucket, currentRsi }
  // Optional thresholds — defaults match frozen rulebook
  function getState(item, { entryThreshold = 30, exitThreshold = 60 } = {}) {

    const symbol      = (item.symbol ?? '').toUpperCase()
    const rsi         = item.currentRsi
    const trend       = item.trendBucket
    const isExcluded  = trend === 'Strong (>300%)'
    const isInTrade   = openSymbols.value.has(symbol)

    // Priority order matters — check blocked first, then in-trade state
    if (isExcluded) {
      return { id: 'blocked', icon: '❌', label: 'Blocked', sublabel: 'Violates Finding #1 — do not trade', class: 'state--blocked' }
    }

    if (isInTrade && rsi != null && rsi > exitThreshold) {
      return { id: 'exit', icon: '🔴', label: 'Exit Signal', sublabel: `RSI ${rsi} — above ${exitThreshold}, consider closing paper trade`, class: 'state--exit' }
    }

    if (isInTrade) {
      return { id: 'in-trade', icon: '🔵', label: 'In Trade', sublabel: `RSI ${rsi ?? '—'} — open paper trade, waiting for RSI > ${exitThreshold}`, class: 'state--in-trade' }
    }

    if (rsi != null && rsi < entryThreshold) {
      return { id: 'entry', icon: '🟢', label: 'Entry Ready', sublabel: `RSI ${rsi} — oversold, validate in Screener then open paper trade`, class: 'state--entry' }
    }

    if (rsi == null) {
      return { id: 'loading', icon: '⚪', label: 'Loading', sublabel: 'Fetching RSI…', class: 'state--idle' }
    }

    return { id: 'idle', icon: '⚪', label: 'Idle', sublabel: `RSI ${rsi} — no signal, waiting for RSI < ${entryThreshold}`, class: 'state--idle' }
  }

  return { openSymbols, loadOpenTrades, invalidateCache, getState }
}