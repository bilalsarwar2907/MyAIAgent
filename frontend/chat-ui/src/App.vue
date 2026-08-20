<template>
  <div id="app">
    <template v-if="authStore.isLoggedIn">

      <div class="top-bar">
        <span class="app-brand">📊 MyAIAgent</span>
        <div class="top-bar-right">
          <span class="top-user">{{ authStore.userName }}</span>
          <button class="theme-btn" @click="themeStore.toggleTheme()">
            {{ themeStore.isDark ? '☀️' : '🌙' }}
          </button>
          <button class="logout-btn" @click="handleLogout">Logout</button>
        </div>
      </div>

      <StockTicker />

      <div class="layout">
        <div class="col-left">
          <ConversationSidebar />
        </div>

        <!-- Chat column — user-controlled, never auto-hidden -->
        <div class="col-chat" :class="{ 'col-chat--collapsed': chatCollapsed }">
          <router-view v-if="!chatCollapsed" />
        </div>

        <!-- Collapse toggle button — sits between chat and right panel -->
        <div class="collapse-gutter" @click="toggleChat" :title="chatCollapsed ? 'Show chat' : 'Hide chat'">
          <button class="collapse-btn" :class="{ 'collapse-btn--collapsed': chatCollapsed }">
            {{ chatCollapsed ? '▶' : '◀' }}
          </button>
        </div>

        <!-- Right panel — always same width, no more auto-expand -->
        <div class="col-right" :class="{ 'col-right--expanded': chatCollapsed }">
          <div class="tabs">
            <button v-for="t in tabs" :key="t.id"
              class="tab" :class="{ active: activeTab === t.id }"
              @click="activeTab = t.id">
              <span class="tab-icon">{{ t.icon }}</span>
              <span class="tab-label">{{ t.label }}</span>
            </button>
          </div>
          <div class="tool-body">
            <WatchlistPanel           v-if="activeTab === 'watch'" />
            <PortfolioPanel           v-if="activeTab === 'portfolio'" />
            <AlertsPanel              v-if="activeTab === 'alerts'" />
            <ResearchPanel            v-if="activeTab === 'research'" />
            <PortfolioResearchPanel   v-if="activeTab === 'multi'" />
            <SectorResearchPanel      v-if="activeTab === 'sectors'" />
            <FactorResearchPanel      v-if="activeTab === 'factors'" />
            <FindingsPanel            v-if="activeTab === 'findings'" />
            <VolatilityResearchPanel  v-if="activeTab === 'volatility'" />
            <ScreenerPanel            v-if="activeTab === 'screener'" />
            <PaperPortfolioPanel      v-if="activeTab === 'paper'" />
            <AnalyticsDashboard       v-if="activeTab === 'analytics'" />
          </div>
        </div>
      </div>

    </template>
    <router-view v-else />
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useThemeStore } from '@/stores/themeStore'
import { useAlertStore } from '@/stores/alertStore'
import ConversationSidebar    from '@/components/ConversationSidebar.vue'
import StockTicker            from '@/components/StockTicker.vue'
import WatchlistPanel         from '@/components/WatchlistPanel.vue'
import PortfolioPanel         from '@/components/PortfolioPanel.vue'
import AlertsPanel            from '@/components/AlertsPanel.vue'
import ResearchPanel          from '@/components/ResearchPanel.vue'
import PortfolioResearchPanel from '@/components/PortfolioResearchPanel.vue'
import SectorResearchPanel    from '@/components/SectorResearchPanel.vue'
import FactorResearchPanel    from '@/components/FactorResearchPanel.vue'
import FindingsPanel          from '@/components/FindingsPanel.vue'
import ScreenerPanel          from '@/components/ScreenerPanel.vue'
import PaperPortfolioPanel    from '@/components/PaperPortfolioPanel.vue'
import AnalyticsDashboard     from '@/components/AnalyticsDashboard.vue'

const authStore  = useAuthStore()
const themeStore = useThemeStore()
const alertStore = useAlertStore()
const router     = useRouter()
const activeTab  = ref('watch')

// ── Chat collapse — user controlled, persisted across sessions ────────────
const COLLAPSE_KEY = 'myaiagent_chat_collapsed'
const chatCollapsed = ref(false)

function toggleChat() {
  chatCollapsed.value = !chatCollapsed.value
  localStorage.setItem(COLLAPSE_KEY, chatCollapsed.value ? '1' : '0')
}

themeStore.initTheme()

onMounted(() => {
  // Restore collapse state from last session
  chatCollapsed.value = localStorage.getItem(COLLAPSE_KEY) === '1'
})

const tabs = [
  { id: 'watch',      icon: '⭐', label: 'Watchlist'  },
  { id: 'portfolio',  icon: '💼', label: 'Real Money' },
  { id: 'alerts',     icon: '🔔', label: 'RSI Alerts' },
  { id: 'research',   icon: '🔍', label: 'Research'   },
  { id: 'multi',      icon: '📊', label: 'Compare'    },
  { id: 'sectors',    icon: '🌐', label: 'Sectors'    },
  { id: 'factors',    icon: '📈', label: 'Factors'    },
  { id: 'findings',   icon: '🏆', label: 'Findings'   },
  //{ id: 'volatility', icon: '📉', label: 'Volatility' },
  { id: 'screener',   icon: '🎯', label: 'Screener'   },
  { id: 'paper',      icon: '📋', label: 'Paper'      },
  { id: 'analytics',  icon: '📊', label: 'Analytics'  },
]

const handleLogout = () => {
  alertStore.stopAutoCheck()
  authStore.logout()
  router.push('/login')
}

const onSwitchTab = (e) => { activeTab.value = e.detail }
window.addEventListener('switch-tab', onSwitchTab)

const onExpandChat = () => {
  chatCollapsed.value = false
  localStorage.setItem(COLLAPSE_KEY, '0')
}
window.addEventListener('expand-chat', onExpandChat)

onUnmounted(() => {
  window.removeEventListener('switch-tab', onSwitchTab)
  window.removeEventListener('expand-chat', onExpandChat)
})
</script>

<style>
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

/* ── CSS variables ── */
:root {
  --bg-page:        #0d0d1a;
  --bg-panel:       #1a1a2e;
  --bg-panel-header:#16213e;
  --bg-panel-item:  #16213e;
  --bg-panel-border:#0f3460;
  --text-primary:   #ffffff;
  --text-secondary: #aaaaaa;
  --text-muted:     #888888;
  --accent:         #e94560;
  --accent-hover:   #c73652;
  --bg-chat:        #12121f;
  --bg-chat-area:   #1a1a2e;
  --bg-msg:         #0d0d1a;
  --text-chat:      #e0e0ee;
}
html.light-theme {
  --bg-page:        #e8e8ec;
  --bg-panel:       #ffffff;
  --bg-panel-header:#f4f4f8;
  --bg-panel-item:  #f7f7fa;
  --bg-panel-border:#ddddee;
  --text-primary:   #1a1a2e;
  --text-secondary: #555566;
  --text-muted:     #888899;
  --accent:         #e94560;
  --accent-hover:   #c73652;
  --bg-chat:        #ffffff;
  --bg-chat-area:   #f4f4f8;
  --bg-msg:         #f4f4f8;
  --text-chat:      #222233;
}

html, body {
  height: 100%;
  overflow: hidden;
  background: var(--bg-page);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  color: var(--text-primary);
}

#app {
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* ── Top bar ── */
.top-bar {
  flex-shrink: 0;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  background: var(--bg-panel);
  border-bottom: 1px solid var(--bg-panel-border);
}
.app-brand     { font-weight: 700; font-size: .95rem; }
.top-bar-right { display: flex; align-items: center; gap: 10px; }
.top-user      { font-size: .78rem; color: var(--text-secondary); }
.theme-btn {
  background: var(--bg-panel-header);
  border: 1px solid var(--bg-panel-border);
  border-radius: 50%; width: 28px; height: 28px;
  cursor: pointer; font-size: .85rem;
  display: flex; align-items: center; justify-content: center;
  transition: border-color .15s, background .15s;
}
.theme-btn:hover { border-color: var(--accent); background: var(--bg-panel-item); }
.logout-btn {
  background: var(--accent); color: #fff; border: none;
  padding: 5px 12px; border-radius: 5px;
  cursor: pointer; font-size: .8rem; font-weight: 600;
}
.logout-btn:hover { background: var(--accent-hover); }

/* ── Layout ── */
.layout {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

/* ── Left sidebar ── */
.col-left {
  flex: 0 0 185px;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  overflow-x: hidden;
  background: var(--bg-panel);
  border-right: 1px solid var(--bg-panel-border);
}

/* ── Chat column ── */
.col-chat {
  flex: 1 1 0;
  min-width: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  transition: flex-basis 0.2s ease, max-width 0.2s ease;
}
.col-chat--collapsed {
  flex: 0 0 0;
  max-width: 0;
  overflow: hidden;
}
.col-chat > * {
  flex: 1;
  min-height: 0;
  width: 100% !important;
  max-width: 100% !important;
  height: 100% !important;
}

/* ── Collapse toggle button ── */
/* ── Collapse gutter + button ── */
.collapse-gutter {
  flex: 0 0 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-panel-header);
  border-left: 1px solid var(--bg-panel-border);
  border-right: 1px solid var(--bg-panel-border);
  cursor: pointer;
  z-index: 10;
  transition: background 0.15s;
}
.collapse-gutter:hover { background: var(--bg-panel-item); }
.collapse-gutter:hover .collapse-btn { color: var(--accent); }

.collapse-btn {
  width: 20px;
  height: 36px;
  background: var(--bg-panel-border);
  border: 1px solid var(--bg-panel-border);
  border-radius: 4px;
  color: var(--text-secondary);
  font-size: 10px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.15s, background 0.15s;
  pointer-events: none;
}
.collapse-btn--collapsed {
  background: rgba(233,69,96,.15);
  border-color: rgba(233,69,96,.3);
  color: var(--accent);
}

/* ── Right panel ── */
.col-right {
  flex: 0 0 380px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--bg-panel);
  border-left: 1px solid var(--bg-panel-border);
  transition: flex 0.2s ease;
}

/* When chat is collapsed, right panel expands to fill the space */
.col-right--expanded {
  flex: 1 1 0;
  min-width: 0;
}

/* ── Tabs — two rows so all 12 fit without scrolling ── */
.tabs {
  flex-shrink: 0;
  display: flex;
  flex-wrap: wrap;
  background: var(--bg-panel-header);
  border-bottom: 1px solid var(--bg-panel-border);
}

.tab {
  flex: 1 1 calc(100% / 6); /* 6 tabs per row = 2 rows for 12 tabs */
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1px;
  padding: 6px 2px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  white-space: nowrap;
  transition: color .15s, border-color .15s, background .15s;
}
.tab:hover  { color: var(--text-primary); background: var(--bg-panel-item); }
.tab.active {
  color: var(--accent);
  border-bottom-color: var(--accent);
  background: rgba(233,69,96,.07);
}
.tab-icon  { font-size: 13px; }
.tab-label { font-size: 8px; font-weight: 600; letter-spacing: .02em; text-transform: uppercase; }

/* ── Tool body ── */
.tool-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 10px;
}
.tool-body > * {
  width: 100% !important;
  max-width: 100% !important;
  min-width: 0 !important;
}
</style>