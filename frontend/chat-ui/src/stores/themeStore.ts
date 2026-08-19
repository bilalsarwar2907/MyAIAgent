import { defineStore } from 'pinia';

export const useThemeStore = defineStore('theme', {
  state: () => ({
    isDark: localStorage.getItem('theme') !== 'light', // default to dark
  }),

  actions: {
    toggleTheme(): void {
      this.isDark = !this.isDark;
      localStorage.setItem('theme', this.isDark ? 'dark' : 'light');
      this.applyTheme();
    },

    // Apply the theme class to the document root
    applyTheme(): void {
      if (this.isDark) {
        document.documentElement.classList.remove('light-theme');
      } else {
        document.documentElement.classList.add('light-theme');
      }
    },

    // Call this once on app startup
    initTheme(): void {
      this.applyTheme();
    },
  },
});