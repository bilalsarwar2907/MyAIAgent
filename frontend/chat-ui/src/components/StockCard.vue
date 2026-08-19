<template>
  <div class="stock-card">

    <!-- Header -->
    <div class="stock-card-header">
      <span class="stock-icon">📊</span>
      <span class="stock-title">Stock Analysis</span>
      <span class="stock-symbols">{{ symbols }}</span>
    </div>

    <!-- Analysis Text -->
    <div class="stock-analysis">
      <div
        v-for="(section, idx) in parsedSections"
        :key="idx"
        class="analysis-section"
      >
        <div v-if="section.type === 'trend'" class="section trend">
          <span class="section-icon">📈</span>
          <div>
            <strong>TREND</strong>
            <p>{{ section.content }}</p>
          </div>
        </div>

        <div v-else-if="section.type === 'momentum'" class="section momentum">
          <span class="section-icon">⚡</span>
          <div>
            <strong>MOMENTUM</strong>
            <p>{{ section.content }}</p>
          </div>
        </div>

        <div v-else-if="section.type === 'volume'" class="section volume">
          <span class="section-icon">📦</span>
          <div>
            <strong>VOLUME</strong>
            <p>{{ section.content }}</p>
          </div>
        </div>

        <div v-else-if="section.type === 'verdict'" class="section verdict">
          <span class="section-icon">🎯</span>
          <div>
            <strong>VERDICT</strong>
            <p>{{ section.content }}</p>
          </div>
        </div>

        <div v-else-if="section.type === 'disclaimer'" class="disclaimer">
          ⚠️ {{ section.content }}
        </div>

        <div v-else class="section general">
          <p>{{ section.content }}</p>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  content: { type: String, required: true },
  symbols: { type: String, default: '' },
});

// Parse the AI analysis text into structured sections
const parsedSections = computed(() => {
  const text = props.content;
  const sections = [];
  const lines = text.split('\n').filter(l => l.trim());

  let currentSection = null;
  let currentContent = [];

  const flush = () => {
    if (currentSection && currentContent.length > 0) {
      sections.push({
        type: currentSection,
        content: currentContent.join(' ').trim(),
      });
      currentContent = [];
    }
  };

  for (const line of lines) {
    const lower = line.toLowerCase();

    if (lower.includes('trend')) {
      flush();
      currentSection = 'trend';
      currentContent.push(line.replace(/trend[:\s]*/i, '').trim());
    } else if (lower.includes('momentum')) {
      flush();
      currentSection = 'momentum';
      currentContent.push(line.replace(/momentum[:\s]*/i, '').trim());
    } else if (lower.includes('volume')) {
      flush();
      currentSection = 'volume';
      currentContent.push(line.replace(/volume[:\s]*/i, '').trim());
    } else if (lower.includes('verdict') || lower.includes('recommend')) {
      flush();
      currentSection = 'verdict';
      currentContent.push(line.replace(/verdict[:\s]*/i, '').trim());
    } else if (lower.includes('not financial advice')) {
      flush();
      currentSection = 'disclaimer';
      currentContent.push(line.trim());
    } else {
      if (currentSection) {
        currentContent.push(line.trim());
      } else {
        sections.push({ type: 'general', content: line.trim() });
      }
    }
  }

  flush();
  return sections;
});
</script>

<style scoped>
.stock-card {
  background: var(--bg-chat);
  border: 1px solid var(--bg-panel-border);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
  max-width: 680px;
}

/* Header */
.stock-card-header {
  background: var(--bg-panel);
  color: var(--text-primary);
  padding: 12px 18px;
  display: flex;
  align-items: center;
  gap: 10px;
}

.stock-icon { font-size: 1.2rem; }

.stock-title {
  font-weight: 700;
  font-size: 0.95rem;
  flex: 1;
}

.stock-symbols {
  background: var(--accent);
  color: #fff;
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 0.8rem;
  font-weight: 600;
}

/* Analysis body */
.stock-analysis {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Sections */
.section {
  display: flex;
  gap: 12px;
  padding: 12px;
  border-radius: 10px;
  align-items: flex-start;
}

.section-icon {
  font-size: 1.3rem;
  margin-top: 2px;
  flex-shrink: 0;
}

.section strong {
  display: block;
  font-size: 0.75rem;
  letter-spacing: 0.05em;
  margin-bottom: 4px;
  opacity: 0.7;
}

.section p {
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.5;
  color: var(--text-primary);
}

.trend     { background: rgba(33, 150, 243, 0.1);  border-left: 3px solid #2196F3; }
.momentum  { background: rgba(255, 193, 7, 0.1);   border-left: 3px solid #FFC107; }
.volume    { background: rgba(156, 39, 176, 0.1);  border-left: 3px solid #9C27B0; }
.verdict   { background: rgba(76, 175, 80, 0.1);   border-left: 3px solid #4CAF50; }

.verdict strong { color: #4CAF50; }

.general p {
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.5;
  color: var(--text-primary);
}

/* Disclaimer */
.disclaimer {
  font-size: 0.78rem;
  color: var(--text-muted);
  border-top: 1px solid var(--bg-panel-border);
  padding-top: 10px;
  font-style: italic;
}
</style>