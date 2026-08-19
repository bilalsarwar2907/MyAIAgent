import { defineStore } from 'pinia';
import api from '@/services/api';

interface PortfolioItem {
  id: number;
  userName: string;
  symbol: string;
  shares: number;
  buyPrice: number;
  note: string;
  boughtAt: string;
  // Live data (fetched separately)
  currentPrice?: number;
  currentValue?: number;
  profitLoss?: number;
  profitLossPercent?: number;
}

interface PortfolioSummary {
  totalInvested: number;
  totalCurrentValue: number;
  totalProfitLoss: number;
  totalProfitLossPercent: number;
}

export const usePortfolioStore = defineStore('portfolio', {
  state: () => ({
    items: [] as PortfolioItem[],
    summary: null as PortfolioSummary | null,
    isLoading: false,
    isRefreshing: false,
    error: null as string | null,
  }),

  actions: {
    // =====================
    // LOAD portfolio from backend
    // =====================
    async loadPortfolio(userName: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        const response = await api.get('/portfolio/' + userName);
        this.items = response.data ?? [];
      } catch (err: unknown) {
        this.error = 'Failed to load portfolio.';
        console.error('[portfolioStore] loadPortfolio error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // ADD a holding
    // =====================
    async addHolding(
      userName: string,
      symbol: string,
      shares: number,
      buyPrice: number,
      note: string = ''
    ): Promise<boolean> {
      this.error = null;

      try {
        const response = await api.post('/portfolio', {
          userName,
          symbol: symbol.toUpperCase(),
          shares,
          buyPrice,
          note,
        });

        this.items.unshift(response.data.item);
        return true;

      } catch (err: unknown) {
        const e = err as any;
        const data = e?.response?.data;
        this.error = (typeof data === 'string' ? data : data?.message) || 'Failed to add holding.';
        return false;
      }
    },

    // =====================
    // REMOVE a holding
    // =====================
    async removeHolding(id: number): Promise<void> {
      this.error = null;

      try {
        await api.delete('/portfolio/' + id);
        this.items = this.items.filter(item => item.id !== id);
        this.calculateSummary();
      } catch (err: unknown) {
        this.error = 'Failed to remove holding.';
      }
    },

    // =====================
    // REFRESH live prices + calculate profit/loss
    // =====================
    async refreshPrices(): Promise<void> {
      if (this.items.length === 0) return;

      this.isRefreshing = true;

      try {
        for (const item of this.items) {
          try {
            const response = await api.get('/stock/' + item.symbol);
            const raw: string = response.data.result || '';

            // Parse price from formatted string "💰 Price: $291.13"
            const priceMatch = raw.match(/Price:\s*\$?([\d.]+)/);

            if (priceMatch?.[1]) {
              const currentPrice = parseFloat(priceMatch[1]);
              item.currentPrice = currentPrice;
              item.currentValue = currentPrice * item.shares;
              item.profitLoss = item.currentValue - (item.buyPrice * item.shares);
              item.profitLossPercent = ((currentPrice - item.buyPrice) / item.buyPrice) * 100;
            }

          } catch (err) {
            console.error('[portfolioStore] refreshPrices error for ' + item.symbol, err);
          }

          // Delay to avoid API rate limit
          await new Promise(resolve => setTimeout(resolve, 1200));
        }

        this.calculateSummary();
      } finally {
        this.isRefreshing = false;
      }
    },

    // =====================
    // CALCULATE overall portfolio summary
    // =====================
    calculateSummary(): void {
      const totalInvested = this.items.reduce(
        (sum, item) => sum + item.buyPrice * item.shares, 0
      );

      const totalCurrentValue = this.items.reduce(
        (sum, item) => sum + (item.currentValue ?? item.buyPrice * item.shares), 0
      );

      const totalProfitLoss = totalCurrentValue - totalInvested;
      const totalProfitLossPercent = totalInvested > 0
        ? (totalProfitLoss / totalInvested) * 100
        : 0;

      this.summary = {
        totalInvested: Math.round(totalInvested * 100) / 100,
        totalCurrentValue: Math.round(totalCurrentValue * 100) / 100,
        totalProfitLoss: Math.round(totalProfitLoss * 100) / 100,
        totalProfitLossPercent: Math.round(totalProfitLossPercent * 100) / 100,
      };
    },
  },
});