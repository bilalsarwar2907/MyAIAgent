<template>
  <div class="ad-panel">

    <!-- Header -->
    <div class="ad-header">
      <div class="ad-header-left">
        <span class="ad-title">📈 Portfolio Analytics</span>
        <span class="ad-subtitle">Equity curve · Compounded returns vs Buy &amp; Hold benchmark</span>
      </div>
      <button class="ad-refresh-btn" :disabled="loading" @click="loadData">
        <span v-if="loading" class="ad-spinner">⟳</span>
        <span v-else>⟳ Refresh</span>
      </button>
    </div>

    <!-- Error -->
    <div v-if="error" class="ad-error">⚠️ {{ error }}</div>

    <!-- Loading -->
    <div v-if="loading" class="ad-loading">
      <div class="ad-loading-text">Loading trades…</div>
    </div>

    <!-- No closed trades yet -->
    <div v-else-if="!loading && closedTrades.length === 0" class="ad-empty">
      <div class="ad-empty-icon">📊</div>
      <div class="ad-empty-title">No closed trades yet</div>
      <div class="ad-empty-desc">
        Close your first paper trade to see the equity curve. The chart plots how
        your portfolio compounds over time compared to simply buying and holding.
      </div>
    </div>

    <!-- Dashboard (has closed trades) -->
    <template v-else>

      <!-- FIX #3 — Evidence level warning -->
      <div v-if="closedTrades.length < 20" class="ad-evidence-banner">
        <span class="ad-evidence-icon">⚠️</span>
        <div>
          <span class="ad-evidence-title">Limited evidence —</span>
          <span class="ad-evidence-text">
            {{ closedTrades.length }} closed trade{{ closedTrades.length !== 1 ? 's' : '' }}.
            {{ closedTrades.length < 5 ? 'No conclusions can be drawn yet.' : 'Patterns may emerge but are not statistically reliable.' }}
            Meaningful validation requires 20–30 closed trades.
          </span>
        </div>
      </div>

      <!-- Hero metrics -->
      <div class="ad-hero-row">

        <!-- FIX #1 — Advantage card: neutral border when no B&H data, not red -->
        <div class="ad-hero-card ad-hero-card--main"
          :class="{
            'ad-hero-card--positive': hasBahData && heroAdvantage > 0,
            'ad-hero-card--negative': hasBahData && heroAdvantage < 0,
            'ad-hero-card--neutral':  !hasBahData || heroAdvantage === 0
          }">
          <div class="ad-hero-label">PORTFOLIO ADVANTAGE vs BUY &amp; HOLD</div>
          <div class="ad-hero-value"
            :class="!hasBahData ? 'ad-muted' : heroAdvantage >= 0 ? 'ad-green' : 'ad-red'">
            {{ hasBahData ? (heroAdvantage >= 0 ? '+' : '') + heroAdvantage.toFixed(1) + 'pp' : '—' }}
          </div>
          <div class="ad-hero-sub">
            {{ hasBahData
              ? 'avg per trade · based on ' + tradesWithBah + ' trade' + (tradesWithBah !== 1 ? 's' : '') + ' with B&H data'
              : 'B&H data pending — close real trades to populate' }}
          </div>
        </div>

        <div class="ad-hero-card">
          <div class="ad-hero-label">PORTFOLIO RETURN</div>
          <div class="ad-hero-value ad-accent">
            {{ compoundedReturn >= 0 ? '+' : '' }}{{ compoundedReturn.toFixed(1) }}%
          </div>
          <div class="ad-hero-sub">compounded across all trades</div>
        </div>

        <div class="ad-hero-card">
          <div class="ad-hero-label">B&amp;H BENCHMARK</div>
          <div class="ad-hero-value ad-muted">
            {{ hasBahData ? (compoundedBenchmark >= 0 ? '+' : '') + compoundedBenchmark.toFixed(1) + '%' : '—' }}
          </div>
          <div class="ad-hero-sub">same periods, buy &amp; hold</div>
        </div>

        <!-- FIX #1 — Win Rate label: "trades beating B&H" is already correct here -->
        <div class="ad-hero-card">
          <div class="ad-hero-label">BEAT B&amp;H RATE</div>
          <div class="ad-hero-value"
            :class="!hasBahData ? 'ad-muted' : winRate >= 55 ? 'ad-green' : winRate >= 45 ? 'ad-yellow' : 'ad-red'">
            {{ hasBahData ? winRate.toFixed(0) + '%' : '—' }}
          </div>
          <div class="ad-hero-sub">trades beating Buy &amp; Hold</div>
        </div>

      </div>

      <!-- Equity Curve Chart -->
      <div class="ad-chart-section">
        <div class="ad-chart-header">
          <span class="ad-chart-title">Equity Curve</span>
          <div class="ad-legend">
            <span class="ad-legend-item">
              <span class="ad-legend-dot ad-legend-dot--portfolio"></span>
              Your Portfolio
            </span>
            <span class="ad-legend-item">
              <span class="ad-legend-dot ad-legend-dot--bah"></span>
              Buy &amp; Hold
            </span>
            <span class="ad-legend-item">
              <span class="ad-legend-dot ad-legend-dot--advantage"></span>
              Advantage
            </span>
          </div>
        </div>

        <!-- SVG chart -->
        <div class="ad-chart-wrap" ref="chartWrap">
          <svg
            :viewBox="`0 0 ${svgW} ${svgH}`"
            class="ad-chart-svg"
            @mousemove="onMouseMove"
            @mouseleave="tooltip.visible = false"
          >
            <!-- Grid lines -->
            <g class="ad-grid">
              <line
                v-for="y in gridYLines"
                :key="'gy' + y"
                :x1="PAD_L" :y1="y" :x2="svgW - PAD_R" :y2="y"
                class="ad-grid-line"
              />
              <line
                v-for="x in gridXLines"
                :key="'gx' + x"
                :x1="x" :y1="PAD_T" :x2="x" :y2="svgH - PAD_B"
                class="ad-grid-line"
              />
            </g>

            <!-- Zero / baseline -->
            <line
              :x1="PAD_L" :y1="yScale(0)" :x2="svgW - PAD_R" :y2="yScale(0)"
              class="ad-zero-line"
            />

            <!-- Advantage area fill (between portfolio and B&H) -->
            <path
              v-if="advantageAreaPath"
              :d="advantageAreaPath"
              class="ad-advantage-area"
            />

            <!-- B&H line -->
            <path
              v-if="bahPath"
              :d="bahPath"
              class="ad-line ad-line--bah"
            />

            <!-- Portfolio line -->
            <path
              v-if="portfolioPath"
              :d="portfolioPath"
              class="ad-line ad-line--portfolio"
            />

            <!-- Y-axis labels -->
            <text
              v-for="tick in yTicks"
              :key="'yt' + tick.value"
              :x="PAD_L - 8"
              :y="tick.y + 4"
              class="ad-axis-label ad-axis-label--y"
            >{{ tick.label }}</text>

            <!-- X-axis labels -->
            <text
              v-for="pt in xAxisPoints"
              :key="'xl' + pt.idx"
              :x="pt.x"
              :y="svgH - PAD_B + 16"
              class="ad-axis-label ad-axis-label--x"
            >{{ pt.label }}</text>

            <!-- Tooltip hit area + dot -->
            <template v-if="tooltip.visible">
              <line
                :x1="tooltip.x" :y1="PAD_T"
                :x2="tooltip.x" :y2="svgH - PAD_B"
                class="ad-tooltip-line"
              />
              <circle
                :cx="tooltip.x" :cy="tooltip.portfolioY"
                r="4" class="ad-dot ad-dot--portfolio"
              />
              <circle
                :cx="tooltip.x" :cy="tooltip.bahY"
                r="4" class="ad-dot ad-dot--bah"
              />
            </template>
          </svg>

          <!-- Tooltip box (HTML overlay) -->
          <div
            v-if="tooltip.visible"
            class="ad-tooltip"
            :style="tooltipStyle"
          >
            <div class="ad-tooltip-date">{{ tooltip.label }}</div>
            <div class="ad-tooltip-row">
              <span class="ad-tooltip-dot ad-tooltip-dot--portfolio"></span>
              <span class="ad-tooltip-key">Portfolio</span>
              <span class="ad-tooltip-val" :class="tooltip.portfolio >= 0 ? 'ad-green' : 'ad-red'">
                {{ tooltip.portfolio >= 0 ? '+' : '' }}{{ tooltip.portfolio.toFixed(1) }}%
              </span>
            </div>
            <div class="ad-tooltip-row">
              <span class="ad-tooltip-dot ad-tooltip-dot--bah"></span>
              <span class="ad-tooltip-key">B&amp;H</span>
              <span class="ad-tooltip-val ad-muted">
                {{ tooltip.bah >= 0 ? '+' : '' }}{{ tooltip.bah.toFixed(1) }}%
              </span>
            </div>
            <div class="ad-tooltip-divider"></div>
            <div class="ad-tooltip-row">
              <span class="ad-tooltip-key">Advantage</span>
              <span class="ad-tooltip-val" :class="tooltip.advantage >= 0 ? 'ad-green' : 'ad-red'">
                {{ tooltip.advantage >= 0 ? '+' : '' }}{{ tooltip.advantage.toFixed(1) }}pp
              </span>
            </div>
          </div>
        </div>

        <!-- Chart note -->
        <div class="ad-chart-note">
          Each point represents a closed trade. Returns are compounded — each trade's gain/loss is applied to the running total.
          The advantage line shows how far ahead (or behind) the portfolio is versus simply holding through each period.
        </div>
      </div>

      <!-- Trade-by-trade table -->
      <div class="ad-section">
        <div class="ad-section-title">Trade-by-Trade Breakdown</div>
        <div class="ad-table-wrap">
          <table class="ad-table">
            <thead>
              <tr>
                <th class="ad-th">#</th>
                <th class="ad-th">Symbol</th>
                <th class="ad-th">Sector</th>
                <th class="ad-th ad-th-num">Closed</th>
                <th class="ad-th ad-th-num">Days</th>
                <th class="ad-th ad-th-num">Trade Return</th>
                <th class="ad-th ad-th-num">B&amp;H Return</th>
                <th class="ad-th ad-th-num">vs B&amp;H</th>
                <th class="ad-th ad-th-num">Portfolio (cum.)</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(row, i) in tableRows"
                :key="row.id"
                class="ad-row"
                :class="row.bah != null ? (row.vsBenchmark >= 0 ? 'ad-row--win' : 'ad-row--loss') : ''"
              >
                <td class="ad-td ad-td-num ad-muted">{{ i + 1 }}</td>
                <td class="ad-td ad-td-symbol">{{ row.symbol }}</td>
                <td class="ad-td ad-td-sector">{{ row.sector }}</td>
                <td class="ad-td ad-td-num ad-td-date">{{ formatDate(row.exitDate) }}</td>
                <td class="ad-td ad-td-num ad-muted">{{ row.daysHeld }}d</td>
                <td class="ad-td ad-td-num">
                  <span :class="row.tradePct >= 0 ? 'ad-green' : 'ad-red'">
                    {{ row.tradePct >= 0 ? '+' : '' }}{{ row.tradePct.toFixed(1) }}%
                  </span>
                </td>
                <!-- FIX #2 — B&H Return: show Pending instead of +0.0% when null -->
                <td class="ad-td ad-td-num">
                  <span v-if="row.bah != null" class="ad-muted">
                    {{ row.bah >= 0 ? '+' : '' }}{{ row.bah.toFixed(1) }}%
                  </span>
                  <span v-else class="ad-pending">Pending</span>
                </td>
                <!-- FIX #2 — vs B&H: show — instead of +0.0pp when no B&H data -->
                <td class="ad-td ad-td-num">
                  <span v-if="row.bah != null" class="ad-bold"
                    :class="row.vsBenchmark >= 0 ? 'ad-green' : 'ad-red'">
                    {{ row.vsBenchmark >= 0 ? '+' : '' }}{{ row.vsBenchmark.toFixed(1) }}pp
                  </span>
                  <span v-else class="ad-muted">—</span>
                </td>
                <td class="ad-td ad-td-num">
                  <span :class="row.cumPortfolio >= 0 ? 'ad-green' : 'ad-red'">
                    {{ row.cumPortfolio >= 0 ? '+' : '' }}{{ row.cumPortfolio.toFixed(1) }}%
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'

const BASE     = 'http://localhost:60363'
const userName = () => localStorage.getItem('userName') ?? 'test2'

// ── Data ───────────────────────────────────────────────────────────────────
const closedTrades = ref([])
const loading      = ref(false)
const error        = ref(null)
const chartWrap    = ref(null)

async function loadData() {
  loading.value = true
  error.value   = null
  try {
    const res  = await fetch(`${BASE}/api/paper/${userName()}`)
    const raw  = await res.text()
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${raw.slice(0, 200)}`)
    const data = JSON.parse(raw)
    closedTrades.value = (data.closedTrades ?? [])
      .filter(t => t.tradePct != null)
      .sort((a, b) => new Date(a.exitDate) - new Date(b.exitDate))
  } catch (e) {
    error.value = 'Failed to load analytics: ' + e.message
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

// ── B&H availability ───────────────────────────────────────────────────────
const tradesWithBah = computed(() =>
  closedTrades.value.filter(t => t.benchmarkBahReturn != null).length
)
const hasBahData = computed(() => tradesWithBah.value > 0)

// ── Computed summary metrics ───────────────────────────────────────────────
const compoundedReturn = computed(() => {
  let val = 100
  for (const t of closedTrades.value) val *= (1 + t.tradePct / 100)
  return val - 100
})

const compoundedBenchmark = computed(() => {
  let val = 100
  for (const t of closedTrades.value) {
    const bah = t.benchmarkBahReturn ?? 0
    val *= (1 + bah / 100)
  }
  return val - 100
})

const heroAdvantage = computed(() => {
  const trades = closedTrades.value.filter(t => t.benchmarkBahReturn != null)
  if (!trades.length) return 0
  return trades.reduce((s, t) => s + (t.tradePct - t.benchmarkBahReturn), 0) / trades.length
})

const winRate = computed(() => {
  const trades = closedTrades.value.filter(t => t.benchmarkBahReturn != null)
  if (!trades.length) return 0
  return (trades.filter(t => t.tradePct > t.benchmarkBahReturn).length / trades.length) * 100
})

// ── Table rows ─────────────────────────────────────────────────────────────
const tableRows = computed(() => {
  let cumPortfolio = 100
  let cumBah       = 100
  return closedTrades.value.map(t => {
    cumPortfolio *= (1 + t.tradePct / 100)
    const bah = t.benchmarkBahReturn
    if (bah != null) cumBah *= (1 + bah / 100)
    return {
      id:           t.id,
      symbol:       t.symbol,
      sector:       t.sector,
      exitDate:     t.exitDate,
      daysHeld:     t.daysHeld,
      tradePct:     t.tradePct,
      bah:          bah,
      // FIX #2 — only calculate vsBenchmark when B&H data exists
      vsBenchmark:  bah != null ? t.tradePct - bah : null,
      cumPortfolio: cumPortfolio - 100,
      cumBah:       cumBah - 100,
    }
  })
})

// ── Chart data points ──────────────────────────────────────────────────────
const chartPoints = computed(() => {
  const pts = [{ portfolio: 0, bah: 0, label: 'Start' }]
  for (const r of tableRows.value) {
    pts.push({
      portfolio: r.cumPortfolio,
      bah:       r.cumBah,
      label:     formatDate(r.exitDate),
    })
  }
  return pts
})

// ── SVG layout constants ───────────────────────────────────────────────────
const svgW  = 800
const svgH  = 300
const PAD_L = 56
const PAD_R = 16
const PAD_T = 16
const PAD_B = 32

// ── Scales ────────────────────────────────────────────────────────────────
const allYValues = computed(() => chartPoints.value.flatMap(p => [p.portfolio, p.bah]))
const yMin = computed(() => Math.min(0, ...allYValues.value))
const yMax = computed(() => Math.max(0, ...allYValues.value))

function yScale(v) {
  const range = yMax.value - yMin.value || 1
  const t = (v - yMin.value) / range
  return svgH - PAD_B - t * (svgH - PAD_T - PAD_B)
}

function xScale(i) {
  const n = chartPoints.value.length
  if (n <= 1) return PAD_L
  return PAD_L + (i / (n - 1)) * (svgW - PAD_L - PAD_R)
}

// ── Grid ──────────────────────────────────────────────────────────────────
const yTicks = computed(() => {
  const range = yMax.value - yMin.value || 10
  const step  = niceStep(range / 5)
  const start = Math.ceil(yMin.value / step) * step
  const ticks = []
  for (let v = start; v <= yMax.value + step * 0.1; v += step) {
    ticks.push({ value: v, y: yScale(v), label: (v >= 0 ? '+' : '') + v.toFixed(0) + '%' })
  }
  return ticks
})

const gridYLines = computed(() => yTicks.value.map(t => t.y))

const gridXLines = computed(() => {
  const n = chartPoints.value.length
  if (n <= 1) return []
  const step = Math.max(1, Math.floor(n / 6))
  const lines = []
  for (let i = 0; i < n; i += step) lines.push(xScale(i))
  return lines
})

const xAxisPoints = computed(() => {
  const pts = chartPoints.value
  const n   = pts.length
  if (n <= 1) return []
  const step = Math.max(1, Math.floor(n / 6))
  const out  = []
  for (let i = 0; i < n; i += step) {
    out.push({ idx: i, x: xScale(i), label: pts[i].label })
  }
  return out
})

function niceStep(rough) {
  const mag = Math.pow(10, Math.floor(Math.log10(rough)))
  for (const f of [1, 2, 2.5, 5, 10]) {
    if (f * mag >= rough) return f * mag
  }
  return mag * 10
}

// ── SVG path builders ─────────────────────────────────────────────────────
const portfolioPath = computed(() => buildPath(chartPoints.value.map(p => p.portfolio)))
const bahPath       = computed(() => buildPath(chartPoints.value.map(p => p.bah)))

function buildPath(values) {
  if (!values.length) return ''
  return values.map((v, i) => `${i === 0 ? 'M' : 'L'} ${xScale(i)} ${yScale(v)}`).join(' ')
}

const advantageAreaPath = computed(() => {
  const pts = chartPoints.value
  if (pts.length < 2) return ''
  const fwd = pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xScale(i)} ${yScale(p.portfolio)}`).join(' ')
  const bwd = pts.slice().reverse().map((p, i, arr) => {
    const origI = arr.length - 1 - i
    return `L ${xScale(origI)} ${yScale(p.bah)}`
  }).join(' ')
  return `${fwd} ${bwd} Z`
})

// ── Tooltip ────────────────────────────────────────────────────────────────
const tooltip = ref({ visible: false, x: 0, portfolioY: 0, bahY: 0, label: '', portfolio: 0, bah: 0, advantage: 0 })

const tooltipStyle = computed(() => {
  const x    = tooltip.value.x
  const wrap = chartWrap.value
  if (!wrap) return {}
  const svgPx = wrap.querySelector('svg')?.getBoundingClientRect()
  if (!svgPx) return {}
  const scaleX = svgPx.width / svgW
  const pxX    = x * scaleX
  const leftOk = pxX + 140 < svgPx.width
  return {
    left:     leftOk ? `${pxX + 12}px` : `${pxX - 152}px`,
    top:      '20px',
    position: 'absolute',
  }
})

function onMouseMove(e) {
  const svg  = e.currentTarget
  const rect = svg.getBoundingClientRect()
  const mx   = ((e.clientX - rect.left) / rect.width) * svgW
  if (mx < PAD_L || mx > svgW - PAD_R) { tooltip.value.visible = false; return }

  const pts = chartPoints.value
  const n   = pts.length
  if (n < 2) return

  let best = 0, bestDist = Infinity
  for (let i = 0; i < n; i++) {
    const d = Math.abs(xScale(i) - mx)
    if (d < bestDist) { bestDist = d; best = i }
  }

  const p = pts[best]
  tooltip.value = {
    visible:    true,
    x:          xScale(best),
    portfolioY: yScale(p.portfolio),
    bahY:       yScale(p.bah),
    label:      p.label,
    portfolio:  p.portfolio,
    bah:        p.bah,
    advantage:  p.portfolio - p.bah,
  }
}

// ── Helpers ────────────────────────────────────────────────────────────────
function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-DK', { day: '2-digit', month: 'short', year: '2-digit' })
}
</script>

<style scoped>
.ad-panel {
  --ad-bg:        var(--bg-panel);
  --ad-bg2:       var(--bg-panel-item);
  --ad-border:    var(--bg-panel-border);
  --ad-text:      var(--text-primary);
  --ad-muted:     var(--text-secondary);
  --ad-accent:    var(--accent);
  --ad-green:     #22c55e;
  --ad-red:       #ef4444;
  --ad-yellow:    #eab308;
  --ad-portfolio: #818cf8;
  --ad-bah:       #64748b;
  --ad-advantage: #34d399;

  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 1rem;
  min-width: 0;
  background: var(--ad-bg);
  color: var(--ad-text);
}

/* Header */
.ad-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.ad-header-left { display: flex; flex-direction: column; gap: 3px; }
.ad-title       { font-size: 15px; font-weight: 700; }
.ad-subtitle    { font-size: 11px; color: var(--ad-muted); }

.ad-refresh-btn {
  background: var(--ad-bg2);
  border: 1px solid var(--ad-border);
  border-radius: 7px;
  padding: 7px 14px;
  font-size: 12px;
  color: var(--ad-text);
  cursor: pointer;
  flex-shrink: 0;
}
.ad-refresh-btn:hover:not(:disabled) { border-color: var(--ad-accent); color: var(--ad-accent); }
.ad-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }

/* Error / Loading / Empty */
.ad-error {
  background: rgba(239,68,68,.1);
  border: 1px solid rgba(239,68,68,.3);
  border-radius: 7px;
  padding: 10px 14px;
  font-size: 12px;
  color: var(--ad-red);
}
.ad-loading      { padding: 40px; text-align: center; }
.ad-loading-text { font-size: 12px; color: var(--ad-muted); }

.ad-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  padding: 48px 24px;
  text-align: center;
}
.ad-empty-icon  { font-size: 36px; opacity: 0.4; }
.ad-empty-title { font-size: 14px; font-weight: 700; }
.ad-empty-desc  { font-size: 12px; color: var(--ad-muted); line-height: 1.6; max-width: 440px; }

/* FIX #3 — Evidence banner */
.ad-evidence-banner {
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
.ad-evidence-icon  { flex-shrink: 0; }
.ad-evidence-title { font-weight: 700; color: var(--ad-text); }
.ad-evidence-text  { color: var(--ad-muted); }

/* Hero metrics */
.ad-hero-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.ad-hero-card {
  background: var(--ad-bg2);
  border: 1px solid var(--ad-border);
  border-radius: 8px;
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 3px;
  flex: 1;
  min-width: 130px;
}

/* FIX #1 — Advantage card states */
.ad-hero-card--main     { background: rgba(99,102,241,.06); }
.ad-hero-card--positive { border-color: var(--ad-green); }
.ad-hero-card--negative { border-color: var(--ad-red); }
.ad-hero-card--neutral  { border-color: var(--ad-border); }

.ad-hero-label { font-size: 9px; font-weight: 700; letter-spacing: 0.07em; color: var(--ad-muted); }
.ad-hero-value { font-size: 26px; font-weight: 800; font-variant-numeric: tabular-nums; line-height: 1.1; }
.ad-hero-sub   { font-size: 10px; color: var(--ad-muted); }

/* Colour helpers */
.ad-green  { color: var(--ad-green); }
.ad-red    { color: var(--ad-red); }
.ad-yellow { color: var(--ad-yellow); }
.ad-muted  { color: var(--ad-muted); }
.ad-accent { color: var(--ad-accent); }
.ad-bold   { font-weight: 700; }

/* FIX #2 — Pending state in table */
.ad-pending {
  font-size: 10px;
  color: var(--ad-muted);
  font-style: italic;
}

/* Chart section */
.ad-chart-section {
  background: var(--ad-bg2);
  border: 1px solid var(--ad-border);
  border-radius: 10px;
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ad-chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}
.ad-chart-title { font-size: 12px; font-weight: 700; }
.ad-legend      { display: flex; gap: 14px; align-items: center; }
.ad-legend-item { display: flex; align-items: center; gap: 5px; font-size: 11px; color: var(--ad-muted); }
.ad-legend-dot  { width: 10px; height: 3px; border-radius: 2px; display: inline-block; }
.ad-legend-dot--portfolio { background: var(--ad-portfolio); height: 3px; }
.ad-legend-dot--bah       { background: var(--ad-bah); height: 2px; }
.ad-legend-dot--advantage { background: var(--ad-advantage); height: 3px; }

.ad-chart-wrap { position: relative; width: 100%; }
.ad-chart-svg  { width: 100%; height: auto; display: block; overflow: visible; }

/* SVG elements */
.ad-grid-line     { stroke: var(--ad-border); stroke-width: 1; stroke-dasharray: 3 4; }
.ad-zero-line     { stroke: var(--ad-muted); stroke-width: 1; opacity: 0.4; }
.ad-advantage-area { fill: var(--ad-advantage); opacity: 0.08; }
.ad-line          { fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
.ad-line--portfolio { stroke: var(--ad-portfolio); stroke-width: 2.5; }
.ad-line--bah       { stroke: var(--ad-bah); stroke-width: 1.5; stroke-dasharray: 5 4; }

.ad-axis-label    { fill: var(--ad-muted); font-size: 9px; font-family: inherit; }
.ad-axis-label--y { text-anchor: end; }
.ad-axis-label--x { text-anchor: middle; }

.ad-tooltip-line { stroke: var(--ad-muted); stroke-width: 1; stroke-dasharray: 3 3; opacity: 0.6; }
.ad-dot          { stroke-width: 2; }
.ad-dot--portfolio { fill: var(--ad-portfolio); stroke: var(--ad-bg); }
.ad-dot--bah       { fill: var(--ad-bah);       stroke: var(--ad-bg); }

/* Tooltip */
.ad-tooltip {
  background: var(--ad-bg);
  border: 1px solid var(--ad-border);
  border-radius: 8px;
  padding: 9px 12px;
  min-width: 140px;
  pointer-events: none;
  z-index: 10;
  box-shadow: 0 4px 16px rgba(0,0,0,.2);
}
.ad-tooltip-date    { font-size: 10px; font-weight: 700; margin-bottom: 6px; color: var(--ad-muted); }
.ad-tooltip-row     { display: flex; align-items: center; gap: 6px; font-size: 11px; margin-bottom: 3px; }
.ad-tooltip-dot     { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.ad-tooltip-dot--portfolio { background: var(--ad-portfolio); }
.ad-tooltip-dot--bah       { background: var(--ad-bah); }
.ad-tooltip-key     { flex: 1; color: var(--ad-muted); }
.ad-tooltip-val     { font-weight: 700; font-variant-numeric: tabular-nums; }
.ad-tooltip-divider { border-top: 1px solid var(--ad-border); margin: 5px 0; }

.ad-chart-note {
  font-size: 10px;
  color: var(--ad-muted);
  line-height: 1.5;
}

/* Table */
.ad-section       { display: flex; flex-direction: column; gap: 8px; }
.ad-section-title { font-size: 12px; font-weight: 700; }

.ad-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--ad-border);
  border-radius: 8px;
}
.ad-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.ad-th {
  background: var(--ad-bg2);
  padding: 8px 11px;
  text-align: left;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.07em;
  color: var(--ad-muted);
  border-bottom: 1px solid var(--ad-border);
  white-space: nowrap;
}
.ad-th-num { text-align: right; }

.ad-td         { padding: 9px 11px; border-bottom: 1px solid var(--ad-border); vertical-align: middle; }
.ad-td-num     { text-align: right; font-variant-numeric: tabular-nums; }
.ad-td-symbol  { font-weight: 700; font-size: 13px; }
.ad-td-sector  { color: var(--ad-muted); font-size: 11px; text-transform: capitalize; }
.ad-td-date    { font-size: 11px; white-space: nowrap; }

.ad-row:hover  { background: var(--ad-bg2); }
.ad-row--win   { background: rgba(34,197,94,.03); }
.ad-row--loss  { background: rgba(239,68,68,.02); }

.ad-spinner { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
</style>