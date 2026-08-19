import { defineStore } from 'pinia';
import api from '@/services/api';

interface PriceAlert {
  id: number;
  userName: string;
  symbol: string;
  targetPrice: number;
  direction: 'above' | 'below';
  isTriggered: boolean;
  triggeredPrice?: number;
  triggeredAt?: string;
  createdAt: string;
}

export const useAlertStore = defineStore('alert', {
  state: () => ({
    alerts: [] as PriceAlert[],
    isLoading: false,
    isChecking: false,
    error: null as string | null,
    checkInterval: null as ReturnType<typeof setInterval> | null,
  }),

  getters: {
    activeAlerts: (state) => state.alerts.filter(a => !a.isTriggered),
    triggeredAlerts: (state) => state.alerts.filter(a => a.isTriggered),
  },

  actions: {
    // =====================
    // LOAD alerts from backend
    // =====================
    async loadAlerts(userName: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        const response = await api.get('/alerts/' + userName);
        this.alerts = response.data;
      } catch (err: unknown) {
        this.error = 'Failed to load alerts.';
        console.error('[alertStore] loadAlerts error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // CREATE a new alert
    // =====================
    async createAlert(
      userName: string,
      symbol: string,
      targetPrice: number,
      direction: 'above' | 'below'
    ): Promise<boolean> {
      this.error = null;

      try {
        const response = await api.post('/alerts', {
          userName,
          symbol: symbol.toUpperCase(),
          targetPrice,
          direction,
        });

        this.alerts.unshift(response.data.alert);
        return true;

      } catch (err: unknown) {
        const e = err as any;
        const data = e?.response?.data;
        this.error = (typeof data === 'string' ? data : data?.message) || 'Failed to create alert.';
        return false;
      }
    },

    // =====================
    // DELETE an alert
    // =====================
    async removeAlert(id: number): Promise<void> {
      this.error = null;

      try {
        await api.delete('/alerts/' + id);
        this.alerts = this.alerts.filter(a => a.id !== id);
      } catch (err: unknown) {
        this.error = 'Failed to remove alert.';
      }
    },

    // =====================
    // CHECK alerts against live prices
    // =====================
    async checkAlerts(userName: string): Promise<PriceAlert[]> {
      if (this.activeAlerts.length === 0) return [];

      this.isChecking = true;

      try {
        const response = await api.post('/alerts/check/' + userName);
        const triggered: PriceAlert[] = response.data.triggered ?? [];

        // Update local state for newly triggered alerts
        if (triggered.length > 0) {
          for (const t of triggered) {
            const idx = this.alerts.findIndex(a => a.id === t.id);
            if (idx !== -1) {
              this.alerts[idx] = t;
            }
          }
        }

        return triggered;

      } catch (err: unknown) {
        console.error('[alertStore] checkAlerts error:', err);
        return [];
      } finally {
        this.isChecking = false;
      }
    },

    // =====================
    // START auto-checking every 60 seconds
    // =====================
    startAutoCheck(userName: string): void {
      // Avoid duplicate intervals
      if (this.checkInterval) return;

      this.checkInterval = setInterval(async () => {
        await this.checkAlerts(userName);
      }, 60000); // 60 seconds
    },

    // =====================
    // STOP auto-checking (e.g. on logout)
    // =====================
    stopAutoCheck(): void {
      if (this.checkInterval) {
        clearInterval(this.checkInterval);
        this.checkInterval = null;
      }
    },
  },
});