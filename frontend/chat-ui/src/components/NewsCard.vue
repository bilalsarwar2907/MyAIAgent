<template>
  <div class="news-card">

    <!-- Header -->
    <div class="news-card-header">
      <span class="news-icon">📰</span>
      <span class="news-title">Latest News</span>
      <span class="news-symbol">{{ symbol }}</span>
    </div>

    <!-- Articles -->
    <div class="news-list">
      <div
        v-for="(article, idx) in parsedArticles"
        :key="idx"
        class="news-item"
      >
        <div class="news-item-top">
          <a v-if="article.link" :href="article.link" target="_blank" rel="noopener noreferrer" class="news-headline">
            {{ article.title }}
          </a>
          <span v-else class="news-headline no-link">{{ article.title }}</span>
          <span
            v-if="article.sentiment"
            class="sentiment-badge"
            :class="getSentimentClass(article.sentiment)"
          >
            {{ article.sentiment }}
          </span>
        </div>

        <div class="news-source">{{ article.source }}</div>

        <p v-if="article.summary" class="news-summary">{{ article.summary }}</p>
      </div>

      <div v-if="parsedArticles.length === 0" class="news-empty">
        {{ content }}
      </div>
    </div>

  </div>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  content: { type: String, required: true },
  symbol: { type: String, default: '' },
});

// Parse the raw text from NewsTool.cs into structured articles
const parsedArticles = computed(() => {
  const text = props.content;
  const articles = [];

  // Split by the 📰 emoji which marks each article start
  const blocks = text.split('📰').slice(1); // skip header before first article

  for (const block of blocks) {
    const lines = block.split('\n').map(l => l.trim()).filter(l => l);

    if (lines.length === 0) continue;

    const title = lines[0];
    let source = '';
    let sentiment = '';
    let summary = '';
    let link = '';

    for (const line of lines) {
      if (line.startsWith('Source:')) {
        source = line.replace('Source:', '').trim();
      } else if (line.startsWith('Sentiment:')) {
        sentiment = line.replace('Sentiment:', '').trim();
      } else if (line.startsWith('Summary:')) {
        summary = line.replace('Summary:', '').trim();
      } else if (line.startsWith('Link:')) {
        link = line.replace('Link:', '').trim();
      }
    }

    articles.push({ title, source, sentiment, summary, link });
  }

  return articles;
});

const getSentimentClass = (sentiment) => {
  const s = sentiment.toLowerCase();
  if (s.includes('bullish') || s.includes('positive')) return 'positive';
  if (s.includes('bearish') || s.includes('negative')) return 'negative';
  return 'neutral';
};
</script>

<style scoped>
.news-card {
  background: var(--bg-chat, #ffffff);
  border: 1px solid var(--bg-panel-border, #e0e0e0);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
  max-width: 680px;
}

.news-card-header {
  background: var(--bg-panel, #1a1a2e);
  color: var(--text-primary);
  padding: 12px 18px;
  display: flex;
  align-items: center;
  gap: 10px;
}

.news-icon { font-size: 1.2rem; }

.news-title {
  font-weight: 700;
  font-size: 0.95rem;
  flex: 1;
}

.news-symbol {
  background: var(--accent, #e94560);
  color: #fff;
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 0.8rem;
  font-weight: 600;
}

.news-list {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.news-item {
  padding: 12px;
  border-radius: 10px;
  background: var(--bg-chat-area, #f9f9f9);
  border: 1px solid var(--bg-panel-border, #eee);
}

.news-item-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
  margin-bottom: 4px;
}

.news-headline {
  font-weight: 600;
  font-size: 0.9rem;
  color: var(--text-chat, #1a1a2e);
  text-decoration: none;
  line-height: 1.4;
}

.news-headline:hover { text-decoration: underline; }
.news-headline.no-link { cursor: default; }
.news-headline.no-link:hover { text-decoration: none; }

.sentiment-badge {
  font-size: 0.68rem;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  white-space: nowrap;
  flex-shrink: 0;
}

.sentiment-badge.positive { background: rgba(46, 125, 50, 0.18); color: #66bb6a; }
.sentiment-badge.negative { background: rgba(198, 40, 40, 0.18); color: #ef5350; }
.sentiment-badge.neutral  { background: rgba(128, 128, 128, 0.15); color: var(--text-secondary); }

.news-source {
  font-size: 0.75rem;
  color: var(--text-muted, #888);
  margin-bottom: 6px;
}

.news-summary {
  font-size: 0.85rem;
  color: var(--text-chat, #444);
  line-height: 1.5;
  margin: 0;
}

.news-empty {
  padding: 16px;
  text-align: center;
  color: var(--text-muted, #888);
  font-size: 0.85rem;
}
</style>