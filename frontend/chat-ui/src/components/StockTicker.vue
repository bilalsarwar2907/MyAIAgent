<template>
  <div class="ticker-bar">
    <div class="ticker-track" :class="{ paused: isPaused }" @mouseenter="isPaused = true" @mouseleave="isPaused = false">
      <div class="ticker-content">
        <span
          v-for="(item, idx) in tickerItems"
          :key="idx"
          class="ticker-item"
        >
          <span class="ticker-symbol">{{ item.symbol }}</span>
          <span v-if="item.price" class="ticker-price" :class="item.changeClass">
            {{ item.price }} {{ item.changeSymbol }}{{ item.changePercent }}
          </span>
          <span v-else class="ticker-price ticker-na">—</span>
        </span>
      </div>
      <!-- Duplicate content for seamless scroll loop -->
      <div class="ticker-content" aria-hidden="true">
        <span
          v-for="(item, idx) in tickerItems"
          :key="'dup-' + idx"
          class="ticker-item"
        >
          <span class="ticker-symbol">{{ item.symbol }}</span>
          <span v-if="item.price" class="ticker-price" :class="item.changeClass">
            {{ item.price }} {{ item.changeSymbol }}{{ item.changePercent }}
          </span>
          <span v-else class="ticker-price ticker-na">—</span>
        </span>
      </div>
    </div>

    <div class="ticker-meta">
      <span class="ticker-updated">{{ lastUpdatedLabel }}</span>
      <button class="ticker-refresh" @click="refresh" :disabled="isRefreshing" title="Refresh prices">
        {{ isRefreshing ? '⏳' : '🔄' }}
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useWatchlistStore } from '@/stores/watchlistStore';
import { usePortfolioStore } from '@/stores/portfolioStore';
import { useAuthStore } from '@/stores/authStore';

const watchlistStore = useWatchlistStore();
const portfolioStore = usePortfolioStore();
const authStore = useAuthStore();

const isPaused = ref(false);
const isRefreshing = ref(false);
const lastUpdated = ref(null);
const tickCount = ref(0);

// Combine watchlist + portfolio symbols, de-duplicated, so the ticker
// reflects what the user actually cares about — no extra API calls of its own.
const tickerItems = computed(() => {
  const seen = new Map();

  for (const item of watchlistStore.items) {
    seen.set(item.symbol, item);
  }
  for (const item of portfolioStore.items) {
    if (!seen.has(item.symbol)) seen.set(item.symbol, item);
  }

  if (seen.size === 0) {
    return [{ symbol: 'Add stocks to your watchlist or portfolio to see them here', price: null }];
  }

  return Array.from(seen.values()).map(item => {
    const raw = item.change !== undefined ? parseFloat(item.change) : null;
    const change = raw !== null && !isNaN(raw) ? raw : null;
    const isUp = change !== null && change >= 0;

    return {
      symbol: item.symbol,
      price: item.currentPrice ? '$' + (typeof item.currentPrice === 'number' ? item.currentPrice.toFixed(2) : item.currentPrice.replace('$', '')) : null,
      changePercent: item.changePercent || '',
      changeSymbol: change === null ? '' : (isUp ? '▲' : '▼'),
      changeClass: change === null ? '' : (isUp ? 'up' : 'down'),
    };
  });
});

const lastUpdatedLabel = computed(() => {
  tickCount.value; // read to create reactivity dependency
  if (!lastUpdated.value) return 'Not refreshed yet';
  const seconds = Math.floor((Date.now() - lastUpdated.value) / 1000);
  if (seconds < 60) return 'Updated ' + seconds + 's ago';
  const minutes = Math.floor(seconds / 60);
  return 'Updated ' + minutes + 'm ago';
});

const refresh = async () => {
  if (isRefreshing.value) return;
  isRefreshing.value = true;

  try {
    // Reuses the existing watchlist/portfolio price-fetch logic —
    // this ticker does not make its own separate API calls.
    await watchlistStore.refreshPrices();
    await portfolioStore.refreshPrices();
    lastUpdated.value = Date.now();
  } finally {
    isRefreshing.value = false;
  }
};

let clockInterval = null;

onMounted(() => {
  // Just re-renders the "Updated Xs ago" label every 10s — does not call any API.
  clockInterval = setInterval(() => {
    tickCount.value++;
  }, 10000);
});

onUnmounted(() => {
  if (clockInterval) clearInterval(clockInterval);
});
</script>

<style scoped>
.ticker-bar {
  display: flex;
  align-items: center;
  background: var(--bg-panel-header);
  border-bottom: 1px solid var(--bg-panel-border);
  overflow: hidden;
  height: 36px;
}

.ticker-track {
  flex: 1;
  display: flex;
  overflow: hidden;
  white-space: nowrap;
  position: relative;
}

.ticker-content {
  display: flex;
  align-items: center;
  animation: scroll-left 30s linear infinite;
  flex-shrink: 0;
}

.ticker-track.paused .ticker-content {
  animation-play-state: paused;
}

@keyframes scroll-left {
  from { transform: translateX(0); }
  to { transform: translateX(-100%); }
}

.ticker-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 18px;
  font-size: 0.78rem;
  border-right: 1px solid var(--bg-panel-border);
}

.ticker-symbol {
  font-weight: 700;
  color: var(--text-primary);
}

.ticker-price {
  color: var(--text-secondary);
}

.ticker-price.up { color: #4caf50; }
.ticker-price.down { color: #f44336; }
.ticker-price.ticker-na { color: var(--text-muted); }

.ticker-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  border-left: 1px solid var(--bg-panel-border);
  flex-shrink: 0;
}

.ticker-updated {
  font-size: 0.68rem;
  color: var(--text-muted);
  white-space: nowrap;
}

.ticker-refresh {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 0.85rem;
  padding: 2px 4px;
  border-radius: 4px;
  transition: background 0.15s;
}
.ticker-refresh:hover:not(:disabled) { background: var(--bg-panel-border); }
.ticker-refresh:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
