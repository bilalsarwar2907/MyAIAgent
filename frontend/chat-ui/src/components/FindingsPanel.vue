<template>
  <div class="fl-panel">

    <!-- Header -->
    <div class="fl-header">
      <div class="fl-header-left">
        <span class="fl-title">🏆 Validated Findings Library</span>
        <span class="fl-subtitle">Research conclusions that have survived hypothesis testing</span>
      </div>
      <div class="fl-header-stats">
        <div class="fl-hs-item">
          <div class="fl-hs-value fl-hs-green">{{ validatedCount }}</div>
          <div class="fl-hs-label">Validated</div>
        </div>
        <div class="fl-hs-item">
          <div class="fl-hs-value fl-hs-red">{{ rejectedCount }}</div>
          <div class="fl-hs-label">Rejected</div>
        </div>
        <div class="fl-hs-item">
          <div class="fl-hs-value fl-hs-yellow">{{ partialCount }}</div>
          <div class="fl-hs-label">Partial</div>
        </div>
        <div class="fl-hs-item">
          <div class="fl-hs-value">{{ findings.length }}</div>
          <div class="fl-hs-label">Total</div>
        </div>
      </div>
    </div>

    <!-- Research Pipeline -->
    <div class="fl-pipeline">
      <div class="fl-pipeline-label">RESEARCH PIPELINE</div>
      <div class="fl-pipeline-stats">
        <div class="fl-pipe-stat"><span class="fl-pipe-value">{{ findings.length }}</span><span class="fl-pipe-key">Hypotheses tested</span></div>
        <div class="fl-pipe-divider">·</div>
        <div class="fl-pipe-stat"><span class="fl-pipe-value fl-hs-green">{{ validatedCount }}</span><span class="fl-pipe-key">Validated</span></div>
        <div class="fl-pipe-divider">·</div>
        <div class="fl-pipe-stat"><span class="fl-pipe-value fl-hs-red">{{ rejectedCount }}</span><span class="fl-pipe-key">Rejected</span></div>
        <div class="fl-pipe-divider">·</div>
        <div class="fl-pipe-stat"><span class="fl-pipe-value fl-hs-yellow">{{ partialCount }}</span><span class="fl-pipe-key">Partial</span></div>
        <div class="fl-pipe-divider">·</div>
        <div class="fl-pipe-stat"><span class="fl-pipe-value" :class="validationRate >= 50 ? 'fl-hs-green' : validationRate >= 25 ? 'fl-hs-yellow' : 'fl-hs-red'">{{ validationRate }}%</span><span class="fl-pipe-key">Validation rate</span></div>
      </div>
    </div>

    <!-- Science note -->
    <div class="fl-science-note">
      <span class="fl-sn-icon">🔬</span>
      <span>A finding is only <strong>Validated</strong> when it survives a second independent time period.
        A finding that worked in one period but failed another is <strong>Rejected</strong> — that's real science, not a failure.
      </span>
    </div>

    <!-- Findings list -->
    <div class="fl-findings">
      <div
        v-for="f in findings"
        :key="f.id"
        class="fl-card"
        :class="`fl-card--${f.status}`"
      >
        <!-- Card top bar -->
        <div class="fl-card-top">
          <div class="fl-card-left">
            <span class="fl-status-badge" :class="`badge--${f.status}`">
              {{ statusIcon(f.status) }} {{ statusLabel(f.status) }}
            </span>
            <span class="fl-card-category">{{ f.category }}</span>
          </div>
          <div class="fl-card-date">{{ f.date }}</div>
        </div>

        <!-- Finding headline -->
        <div class="fl-card-headline">{{ f.headline }}</div>

        <!-- Key numbers -->
        <div class="fl-card-numbers" v-if="f.numbers && f.numbers.length">
          <div
            v-for="n in f.numbers"
            :key="n.label"
            class="fl-number-chip"
            :class="n.positive === true ? 'chip-positive' : n.positive === false ? 'chip-negative' : 'chip-neutral'"
          >
            <div class="fl-number-value">{{ n.value }}</div>
            <div class="fl-number-label">{{ n.label }}</div>
          </div>
        </div>

        <!-- Description -->
        <div class="fl-card-desc">{{ f.description }}</div>

        <!-- Period evidence -->
        <div class="fl-periods" v-if="f.periods && f.periods.length">
          <div class="fl-periods-label">EVIDENCE ACROSS PERIODS</div>
          <div class="fl-periods-row">
            <div
              v-for="p in f.periods"
              :key="p.label"
              class="fl-period-item"
              :class="p.held ? 'period-held' : 'period-failed'"
            >
              <div class="fl-period-label">{{ p.label }}</div>
              <div class="fl-period-result">{{ p.held ? '✅ Held' : '❌ Failed' }}</div>
              <div class="fl-period-detail">{{ p.detail }}</div>
            </div>
          </div>
        </div>

        <!-- What this means for trading -->
        <div class="fl-implication">
          <span class="fl-impl-label">IMPLICATION</span>
          <span class="fl-impl-text">{{ f.implication }}</span>
        </div>
      </div>
    </div>

    <!-- Bottom note -->
    <div class="fl-footer">
      Findings are added manually after completing sector, factor, or multi-period research runs.
      Only conclusions that have been stress-tested against an independent period qualify as Validated.
    </div>

  </div>
</template>

<script setup>
const findings = [
  {
    id: 1,
    status: 'validated',
    category: 'Trend Strength · Factor Research',
    date: 'Jun 2026',
    headline: 'RSI consistently fails on strong-trend stocks — across two independent decades.',
    numbers: [
      { value: '0/22', label: 'Strong-trend stocks beat B&H', positive: false },
      { value: '2016–2026', label: 'First period tested', positive: null },
      { value: '2006–2016', label: 'Validation period', positive: null },
    ],
    description:
      'Strong-trend stocks (buy-and-hold return >300%) produced zero RSI wins in both the 2016–2026 ' +
      'primary period and the independent 2006–2016 validation period. The pattern is not regime-specific — ' +
      'it held through the 2008 financial crisis and the 2014–2016 oil collapse.',
    periods: [
      { label: '2016–2026', held: true,  detail: '0/11 strong-trend stocks beat B&H' },
      { label: '2006–2016', held: true,  detail: '0/11 strong-trend stocks beat B&H' },
    ],
    implication:
      'Never apply RSI mean-reversion to a stock that has been in a strong multi-year uptrend. ' +
      'Momentum overrides the mean-reversion signal. Screener rule: exclude stocks with >300% 10-year return.',
  },
  {
    id: 2,
    status: 'rejected',
    category: 'Trend Strength · Factor Research',
    date: 'Jun 2026',
    headline: 'Weak-trend RSI advantage did not survive the second period — regime-specific, not structural.',
    numbers: [
      { value: '75.9%', label: 'Win rate 2016–2026', positive: true  },
      { value: '22.2%', label: 'Win rate 2006–2016', positive: false },
      { value: '−53.7pp', label: 'Drop between periods', positive: false },
    ],
    description:
      'Weak-trend stocks (<100% buy-and-hold return) showed a strong RSI edge in 2016–2026 (75.9% beat B&H). ' +
      'When validated against 2006–2016 — a period including the financial crisis — the win rate collapsed to 22.2%. ' +
      'The advantage was driven by the 2016–2026 market regime, not an enduring structural edge.',
    periods: [
      { label: '2016–2026', held: true,  detail: '22/29 weak-trend stocks beat B&H (75.9%)' },
      { label: '2006–2016', held: false, detail: 'Only 6/27 weak-trend stocks beat B&H (22.2%)' },
    ],
    implication:
      'Do not use weak-trend classification alone as a buy signal for RSI strategies. ' +
      'The 2016–2026 result was likely a low-volatility bull market artifact. Further factor isolation required.',
  },
  {
    id: 3,
    status: 'partial',
    category: 'Sector Research · Cross-Period Validation',
    date: 'Jun 2026',
    headline: 'Energy sector RSI edge survived both periods. Airlines did not.',
    numbers: [
      { value: '5/6',   label: 'Energy beat B&H (2016–2026)', positive: true  },
      { value: '3/6',   label: 'Energy beat B&H (2006–2016)', positive: true  },
      { value: '5/6 → 1/6', label: 'Airlines across periods', positive: false },
    ],
    description:
      'Of 10 sectors tested, only Energy maintained a positive RSI edge across both independent decades. ' +
      'Airlines looked strong in 2016–2026 (5/6 stocks beat B&H, +41.6% median advantage) but collapsed ' +
      'in 2006–2016 (1/6, −81%). The Airlines edge was regime-specific — driven by the post-COVID recovery. ' +
      'Energy\'s cyclicality appears to create genuine RSI opportunities independent of market regime.',
    periods: [
      { label: 'Energy 2016–2026',   held: true,  detail: '5/6 beat B&H · +36.7% median adv.' },
      { label: 'Energy 2006–2016',   held: true,  detail: '3/6 beat B&H · +4.4% median adv.' },
      { label: 'Airlines 2016–2026', held: true,  detail: '5/6 beat B&H · +41.6% median adv.' },
      { label: 'Airlines 2006–2016', held: false, detail: '1/6 beat B&H · −81% median adv.' },
    ],
    implication:
      'Energy is the only sector currently warranting further RSI research. ' +
      'Airlines should be excluded until a structural (non-regime) explanation is identified. ' +
      'All other sectors showed no durable edge.',
  },
  {
    id: 4,
    status: 'rejected',
    category: 'Volatility · Factor Research',
    date: 'Jun 2026',
    headline: 'Volatility is not a reliable predictor of RSI effectiveness — high-vol stocks outperformed low-vol.',
    numbers: [
      { value: '23.1%', label: 'Low-vol win rate (<25%)', positive: false },
      { value: '50.0%', label: 'High-vol win rate (≥50%)', positive: null },
      { value: '−19.5%', label: 'Low-vol median advantage', positive: false },
    ],
    description:
      'Hypothesis: RSI mean-reversion works better on low-volatility stocks than high-volatility stocks. ' +
      'Primary period (2016–2026): Low-vol stocks had only a 23.1% win rate and −19.5% median advantage. ' +
      'High-vol stocks (≥50% annualised vol) actually outperformed at 50.0% win rate — the opposite of the hypothesis. ' +
      'Validation period (2006–2016) confirmed the same direction: Low-vol 15.4% vs High-vol 25.0%. ' +
      'The hypothesis failed in both periods and in the wrong direction.',
    periods: [
      { label: 'Low-vol 2016–2026',  held: false, detail: '23.1% beat B&H · −19.5% median adv.' },
      { label: 'Med-vol 2016–2026',  held: false, detail: '29.3% beat B&H · −53.9% median adv.' },
      { label: 'High-vol 2016–2026', held: false, detail: '50.0% beat B&H · −74.3% median adv.' },
      { label: 'Low-vol 2006–2016',  held: false, detail: '15.4% beat B&H · confirmed rejection' },
      { label: 'High-vol 2006–2016', held: false, detail: '25.0% beat B&H · same direction' },
    ],
    implication:
      'Do not use volatility buckets as a filter for RSI candidate selection. ' +
      'Volatility is not a reliable predictor of RSI effectiveness in either direction. ' +
      'Screener note: volatility column may be shown for informational context only — not as a signal.',
  },
]

const validatedCount = findings.filter(f => f.status === 'validated').length
const rejectedCount  = findings.filter(f => f.status === 'rejected').length
const partialCount   = findings.filter(f => f.status === 'partial').length
const validationRate = findings.length > 0 ? Math.round(validatedCount / findings.length * 100) : 0

const statusIcon = s => ({ validated: '✅', rejected: '❌', partial: '⚠️' }[s] ?? '—')
const statusLabel = s => ({ validated: 'Validated', rejected: 'Rejected', partial: 'Partial' }[s] ?? s)
</script>

<style scoped>
.fl-panel {
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
  gap: 12px;
  padding: 1rem;
  min-width: 0;
  background: var(--color-bg);
  color: var(--color-text);
}

/* ── Header ── */
.fl-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.fl-header-left { display: flex; flex-direction: column; gap: 3px; }
.fl-title    { font-size: 15px; font-weight: 700; }
.fl-subtitle { font-size: 11px; color: var(--color-muted); }

.fl-header-stats {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}
.fl-hs-item {
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-radius: 7px;
  padding: 6px 12px;
  text-align: center;
  min-width: 56px;
}
.fl-hs-value  { font-size: 18px; font-weight: 700; font-variant-numeric: tabular-nums; }
.fl-hs-label  { font-size: 9px; color: var(--color-muted); font-weight: 600; letter-spacing: 0.06em; text-transform: uppercase; margin-top: 1px; }
.fl-hs-green  { color: var(--color-positive); }
.fl-hs-red    { color: var(--color-negative); }
.fl-hs-yellow { color: var(--color-partial); }

/* ── Research Pipeline ── */
.fl-pipeline {
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 10px 14px;
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.fl-pipeline-label {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.1em;
  color: var(--color-muted);
  flex-shrink: 0;
}
.fl-pipeline-stats {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  flex: 1;
}
.fl-pipe-stat {
  display: flex;
  align-items: baseline;
  gap: 4px;
}
.fl-pipe-value {
  font-size: 16px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.fl-pipe-key {
  font-size: 10px;
  color: var(--color-muted);
}
.fl-pipe-divider {
  color: var(--color-border);
  font-size: 14px;
}

/* ── Science note ── */
.fl-science-note {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  background: var(--color-bg2);
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-accent);
  border-radius: 7px;
  padding: 10px 12px;
  font-size: 12px;
  color: var(--color-muted);
  line-height: 1.6;
}
.fl-sn-icon { font-size: 14px; flex-shrink: 0; margin-top: 1px; }

/* ── Finding cards ── */
.fl-findings { display: flex; flex-direction: column; gap: 12px; }

.fl-card {
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--color-bg2);
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.fl-card--validated { border-left: 4px solid var(--color-positive); }
.fl-card--rejected  { border-left: 4px solid var(--color-negative); }
.fl-card--partial   { border-left: 4px solid var(--color-partial); }

/* Card top */
.fl-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}
.fl-card-left    { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.fl-card-date    { font-size: 10px; color: var(--color-muted); flex-shrink: 0; }
.fl-card-category { font-size: 10px; color: var(--color-muted); font-weight: 600; letter-spacing: 0.04em; }

.fl-status-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 4px;
  white-space: nowrap;
  letter-spacing: 0.04em;
}
.badge--validated { background: rgba(34,197,94,.15);  color: var(--color-positive); }
.badge--rejected  { background: rgba(239,68,68,.12);  color: var(--color-negative); }
.badge--partial   { background: rgba(234,179,8,.12);  color: var(--color-partial); }

/* Headline */
.fl-card-headline {
  font-size: 13px;
  font-weight: 700;
  line-height: 1.4;
}

/* Key numbers */
.fl-card-numbers {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.fl-number-chip {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 7px;
  padding: 7px 12px;
  text-align: center;
  min-width: 80px;
}
.fl-number-value  { font-size: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.fl-number-label  { font-size: 9px; color: var(--color-muted); margin-top: 2px; line-height: 1.3; }
.chip-positive .fl-number-value { color: var(--color-positive); }
.chip-negative .fl-number-value { color: var(--color-negative); }
.chip-neutral  .fl-number-value { color: var(--color-text); }

/* Description */
.fl-card-desc {
  font-size: 12px;
  line-height: 1.65;
  color: var(--color-muted);
}

/* Period evidence */
.fl-periods { display: flex; flex-direction: column; gap: 6px; }
.fl-periods-label {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.1em;
  color: var(--color-muted);
}
.fl-periods-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 6px;
}
.fl-period-item {
  background: var(--color-bg);
  border-radius: 6px;
  padding: 8px 10px;
  border-left: 3px solid var(--color-border);
}
.period-held   { border-left-color: var(--color-positive); }
.period-failed { border-left-color: var(--color-negative); }

.fl-period-label  { font-size: 10px; font-weight: 700; color: var(--color-muted); margin-bottom: 2px; }
.fl-period-result { font-size: 11px; font-weight: 700; margin-bottom: 2px; }
.period-held   .fl-period-result { color: var(--color-positive); }
.period-failed .fl-period-result { color: var(--color-negative); }
.fl-period-detail { font-size: 10px; color: var(--color-muted); line-height: 1.4; }

/* Implication */
.fl-implication {
  background: var(--color-bg);
  border-radius: 6px;
  padding: 9px 12px;
  font-size: 12px;
  line-height: 1.6;
  display: flex;
  gap: 8px;
  align-items: flex-start;
}
.fl-impl-label {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.1em;
  color: var(--color-accent);
  flex-shrink: 0;
  padding-top: 2px;
}
.fl-impl-text { color: var(--color-text); }

/* Footer */
.fl-footer {
  font-size: 10px;
  color: var(--color-muted);
  line-height: 1.6;
  text-align: center;
  padding-top: 4px;
  border-top: 1px solid var(--color-border);
  padding-top: 10px;
}
</style>