import { defineStore } from 'pinia';
import api from '@/services/api';

interface WatchlistItem {
  id: number;
  userName: string;
  symbol: string;
  note: string;
  addedAt: string;
  currentPrice?: string;
  change?: string;
  changePercent?: string;
}

export const useWatchlistStore = defineStore('watchlist', {
  state: () => ({
    items: [] as WatchlistItem[],
    isLoading: false,
    isRefreshing: false,
    error: null as string | null,
  }),

  actions: {
    // =====================
    // LOAD watchlist from backend
    // =====================
    async loadWatchlist(userName: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        const response = await api.get('/watchlist/' + userName);
        this.items = response.data ?? [];
      } catch (err: unknown) {
        this.error = 'Failed to load watchlist.';
        console.error('[watchlistStore] loadWatchlist error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // ADD stock to watchlist
    // =====================
    async addStock(userName: string, symbol: string, note: string = ''): Promise<boolean> {
      this.error = null;

      try {
        const response = await api.post('/watchlist', {
          userName: userName,
          symbol: symbol.toUpperCase(),
          note: note,
        });

        // Add to local state immediately
        this.items.unshift(response.data.item);
        return true;

      } catch (err: unknown) {
        const e = err as any;
        const data = e?.response?.data;
        this.error = (typeof data === 'string' ? data : data?.message) || 'Failed to add stock.';
        console.error('[watchlistStore] addStock error:', err);
        return false;
      }
    },

    // =====================
    // REMOVE stock from watchlist
    // =====================
    async removeStock(id: number): Promise<void> {
      this.error = null;

      try {
        await api.delete('/watchlist/' + id);
        // Remove from local state immediately
        this.items = this.items.filter(item => item.id !== id);
      } catch (err: unknown) {
        this.error = 'Failed to remove stock.';
        console.error('[watchlistStore] removeStock error:', err);
      }
    },

    // =====================
    // REFRESH live prices for all watchlist stocks
    // =====================
    async refreshPrices(): Promise<void> {
      if (this.items.length === 0) return;

      this.isRefreshing = true;

      try {
        for (const item of this.items) {
          try {
            const response = await api.get('/stock/' + item.symbol);
            const raw: string = response.data.result || '';

            // Parse price from the formatted string e.g. "💰 Price: $291.13"
            const priceMatch = raw.match(/Price:\s*\$?([\d.]+)/);
            const changeMatch = raw.match(/Change:\s*([-\d.]+)/);
            const pctMatch = raw.match(/\(([-\d.]+%)\)/);

            if (priceMatch) item.currentPrice = '$' + priceMatch[1];
            if (changeMatch) item.change = changeMatch[1];
            if (pctMatch) item.changePercent = pctMatch[1];

          } catch (err) {
            console.error('[watchlistStore] refreshPrices error for ' + item.symbol, err);
          }

          // Small delay to avoid hitting API rate limit
          await new Promise(resolve => setTimeout(resolve, 1200));
        }
      } finally {
        this.isRefreshing = false;
      }
    },
  },
});