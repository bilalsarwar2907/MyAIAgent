<template>
  <div class="decision-card">

    <!-- Header -->
    <div class="decision-header">
      <span class="decision-icon">📋</span>
      <span class="decision-title">Decision Check</span>
      <span class="decision-symbol">{{ symbol }}</span>
    </div>

    <!-- Error state -->
    <div v-if="error" class="decision-error">
      ⚠️ {{ error }}
    </div>

    <!-- Table -->
    <div v-else class="decision-table">
      <div
        v-for="(row, idx) in rows"
        :key="idx"
        class="decision-row"
        :class="row.passed ? 'pass' : 'fail'"
      >
        <div class="row-top">
          <span class="row-factor">{{ row.factor }}</span>
          <span class="row-status" :class="row.passed ? 'pass' : 'fail'">
            {{ row.passed ? 'Pass' : 'Fail' }}
          </span>
        </div>
        <div class="row-value">{{ row.value }}</div>
        <div class="row-rule">
          <span class="rule-label">Rule:</span> {{ row.rule }}
        </div>
        <div class="row-result">{{ row.result }}</div>
      </div>

      <!-- Verdict -->
      <div class="decision-verdict">
        <div class="verdict-top">
          <span class="verdict-label">Verdict</span>
          <span class="verdict-count">{{ passedCount }} of {{ rows.length }} buy rules met</span>
        </div>
        <div class="verdict-action">{{ verdictAction }}</div>
      </div>
    </div>

    <div class="decision-disclaimer">
      ⚠️ Rule-based check on real data, not financial advice. Past patterns don't guarantee future results.
    </div>

  </div>
</template>

<script setup>
defineProps({
  symbol: { type: String, default: '' },
  rows: { type: Array, default: () => [] },
  passedCount: { type: Number, default: 0 },
  verdictAction: { type: String, default: '' },
  error: { type: String, default: '' },
});
</script>

<style scoped>
.decision-card {
  background: var(--bg-chat, #ffffff);
  border: 1px solid var(--bg-panel-border, #e0e0e0);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
  max-width: 680px;
}

.decision-header {
  background: var(--bg-panel, #1a1a2e);
  color: white;
  padding: 12px 18px;
  display: flex;
  align-items: center;
  gap: 10px;
}

.decision-icon { font-size: 1.2rem; }

.decision-title {
  font-weight: 700;
  font-size: 0.95rem;
  flex: 1;
}

.decision-symbol {
  background: var(--accent, #e94560);
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 0.8rem;
  font-weight: 600;
}

.decision-error {
  padding: 16px;
  color: #ff6b6b;
  font-size: 0.88rem;
}

.decision-table {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.decision-row {
  padding: 12px;
  border-radius: 10px;
  border-left: 3px solid transparent;
  background: var(--bg-chat-area, #f9f9f9);
}

.decision-row.pass { border-left-color: #4caf50; }
.decision-row.fail { border-left-color: #f44336; }

.row-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.row-factor {
  font-weight: 600;
  font-size: 0.9rem;
  color: var(--text-chat, #1a1a2e);
}

.row-status {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 10px;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.row-status.pass { background: rgba(46, 125, 50, 0.18); color: #66bb6a; }
.row-status.fail { background: rgba(198, 40, 40, 0.18); color: #ef5350; }

.row-value {
  font-size: 0.83rem;
  color: var(--text-muted, #888);
  margin-bottom: 6px;
}

.row-rule {
  font-size: 0.78rem;
  color: var(--text-secondary, #666);
  line-height: 1.45;
  margin-bottom: 6px;
}

.rule-label {
  font-weight: 600;
  color: var(--text-chat, #444);
}

.row-result {
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-chat, #333);
  line-height: 1.4;
}

/* Verdict */
.decision-verdict {
  margin-top: 4px;
  padding: 14px;
  border-radius: 10px;
  background: var(--bg-panel-header, #16213e);
  color: var(--text-primary);
}

.verdict-top {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 8px;
}

.verdict-label {
  font-weight: 700;
  font-size: 0.85rem;
  letter-spacing: 0.03em;
}

.verdict-count {
  font-size: 0.78rem;
  opacity: 0.8;
}

.verdict-action {
  font-size: 0.85rem;
  line-height: 1.5;
}

.decision-disclaimer {
  font-size: 0.74rem;
  color: var(--text-muted, #888);
  padding: 10px 16px 14px;
  font-style: italic;
}
</style>