<template>
  <div class="chat-wrapper">

    <div class="chat-header">
      <h2>🤖 AI Agent</h2>
      <button class="new-chat-btn" @click="startNewChat">+ New Chat</button>
    </div>

    <div class="decision-trigger">
      <input v-model="decisionSymbol" placeholder="Symbol e.g. AAPL" @keyup.enter="checkDecision" />
      <button @click="checkDecision" :disabled="decisionLoading">
        {{ decisionLoading ? 'Checking...' : 'Run decision check (4 requests)' }}
      </button>
    </div>

    <div v-if="decisionResult" class="decision-result-wrap">
      <DecisionTable
        :symbol="decisionSymbol.toUpperCase()"
        :rows="decisionResult.rows || []"
        :passed-count="decisionResult.passedCount || 0"
        :verdict-action="decisionResult.verdictAction || ''"
        :error="decisionResult.error || ''"
      />
      <button class="decision-close" @click="decisionResult = null">Close</button>
    </div>

    <div class="message-list" ref="messageList">
      <div v-if="chatStore.messages.length === 0" class="empty-state">
        <p>👋 Hello! Ask me anything about stocks or programming.</p>
        <div class="suggestions">
          <button @click="suggest('analyze AAPL and MSFT and tell me which is better')">📊 Compare AAPL vs MSFT</button>
          <button @click="suggest('should I buy TSLA right now?')">🤔 Should I buy TSLA?</button>
          <button @click="suggest('what is the price of GOOGL')">💰 Price of GOOGL</button>
          <button @click="suggest('explain what RSI means in stocks')">📚 What is RSI?</button>
        </div>
      </div>

      <div
        v-for="(msg, idx) in chatStore.messages"
        :key="idx"
        :class="['message', msg.role]"
      >
        <div v-if="msg.role === 'user'" class="message-bubble user-bubble">
          <strong>🧑 You</strong>
          <p>{{ msg.content }}</p>
        </div>

        <div v-else-if="msg.role === 'assistant' && msg.type === 'analysis'" class="message-bubble assistant-bubble">
          <StockCard :content="msg.content" :symbols="msg.symbols || ''" />
        </div>

        <div v-else-if="msg.role === 'assistant' && msg.type === 'news'" class="message-bubble assistant-bubble">
          <NewsCard :content="msg.content" :symbol="msg.symbols || ''" />
        </div>

        <div v-else-if="msg.role === 'assistant' && msg.type === 'price'" class="message-bubble assistant-bubble price-bubble">
          <strong>🤖 AI</strong>
          <pre>{{ msg.content }}</pre>
        </div>

        <div v-else class="message-bubble assistant-bubble">
          <strong>🤖 AI</strong>
          <p class="ai-text">{{ msg.content }}</p>
        </div>
      </div>

      <div v-if="chatStore.isLoading" class="message assistant">
        <div class="message-bubble assistant-bubble loading">
          <span>🤖 AI is thinking</span>
          <span class="dots">...</span>
          <span class="loading-hint">{{ loadingHint }}</span>
        </div>
      </div>
    </div>

    <div v-if="chatStore.error" class="error-banner">
      ⚠️ {{ chatStore.error }}
    </div>

    <div class="input-area">
      <input
        v-model="userInput"
        @keyup.enter="sendMessage"
        placeholder="Ask about stocks or programming..."
        :disabled="chatStore.isLoading"
        ref="inputField"
      />
      <button
        @click="sendMessage"
        :disabled="chatStore.isLoading || !userInput.trim()"
      >
        {{ chatStore.isLoading ? '...' : 'Send' }}
      </button>
    </div>

  </div>
</template>

<script setup>
import { ref, nextTick, watch, computed } from 'vue';
import { useChatStore } from '@/stores/chatStore';
import StockCard from '@/components/StockCard.vue';
import NewsCard from '@/components/NewsCard.vue';
import DecisionTable from '@/components/DecisionTable.vue';
import api from '@/services/api';

const chatStore = useChatStore();
const userInput = ref('');
const messageList = ref(null);
const inputField = ref(null);

chatStore.initConversation();

const loadingHint = computed(() => {
  return chatStore.isAnalyzing
    ? '(fetching market data + running AI analysis...)'
    : '';
});

watch(
  () => chatStore.messages.length,
  async () => {
    await nextTick();
    if (messageList.value) {
      messageList.value.scrollTop = messageList.value.scrollHeight;
    }
  }
);

const sendMessage = async () => {
  if (!userInput.value.trim() || chatStore.isLoading) return;
  const msg = userInput.value;
  userInput.value = '';
  await chatStore.sendMessage(msg);
  inputField.value?.focus();
};

const suggest = (text) => {
  userInput.value = text;
  sendMessage();
};

const startNewChat = () => {
  chatStore.newConversation();
};

// --- Decision check ---
const decisionSymbol = ref('');
const decisionResult = ref(null);
const decisionLoading = ref(false);

const checkDecision = async () => {
  if (!decisionSymbol.value.trim()) return;
  decisionLoading.value = true;
  decisionResult.value = null;
  try {
    const response = await api.get('/decision/' + decisionSymbol.value.trim().toUpperCase());
    decisionResult.value = response.data;
  } catch (err) {
    decisionResult.value = { error: 'Request failed. Check backend logs.' };
  } finally {
    decisionLoading.value = false;
  }
};
</script>

<style scoped>

.chat-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  max-width: 100%;
  margin: 0;
  border: none;
  border-radius: 0;
  overflow: hidden;
  background: var(--bg-chat);
  box-shadow: none;
}
.chat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
  background: var(--bg-panel);
  color: var(--text-primary);
  transition: background 0.3s, color 0.3s;
}

.chat-header h2 { margin: 0; font-size: 1.2rem; }

.new-chat-btn {
  background: var(--accent);
  color: white;
  border: none;
  padding: 6px 14px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.85rem;
}
.new-chat-btn:hover { background: var(--accent-hover); }

/* Decision check trigger — lives directly under the header, above the chat thread */
.decision-trigger {
  display: flex;
  gap: 8px;
  padding: 10px 16px;
  background: var(--bg-chat-area);
  border-bottom: 1px solid var(--bg-panel-border);
  flex-shrink: 0;
}
.decision-trigger input {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid var(--bg-panel-border);
  border-radius: 6px;
  background: var(--bg-panel-item);
  color: var(--text-chat);
  font-size: 0.85rem;
  outline: none;
  transition: border-color 0.2s;
}
.decision-trigger input::placeholder { color: var(--text-muted); }
.decision-trigger input:focus { border-color: var(--accent); }
.decision-trigger button {
  padding: 6px 14px;
  background: var(--accent);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 0.8rem;
  cursor: pointer;
  white-space: nowrap;
}
.decision-trigger button:disabled { opacity: 0.6; cursor: not-allowed; }

.decision-result-wrap {
  padding: 14px 16px;
  background: var(--bg-chat-area);
  border-bottom: 1px solid var(--bg-panel-border);
  flex-shrink: 0;
  max-height: 50vh;
  overflow-y: auto;
}
.decision-close {
  margin-top: 8px;
  padding: 5px 12px;
  background: var(--bg-panel-border);
  color: var(--text-chat);
  border: none;
  border-radius: 6px;
  font-size: 0.78rem;
  cursor: pointer;
  transition: background 0.15s;
}
.decision-close:hover { background: var(--text-muted); }

.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  background: var(--bg-chat);
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: background 0.3s;
}

.empty-state {
  text-align: center;
  color: var(--text-muted);
  margin-top: 30px;
}

.suggestions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: center;
  margin-top: 16px;
}

.suggestions button {
  background: var(--bg-chat);
  border: 1px solid var(--bg-panel-border);
  border-radius: 20px;
  padding: 8px 14px;
  cursor: pointer;
  font-size: 0.85rem;
  color: var(--text-chat);
  transition: all 0.2s;
}

.suggestions button:hover {
  background: var(--bg-panel);
  color: var(--text-primary);
  border-color: var(--accent);
}

.message { display: flex; margin-bottom: 4px; }
.message.user { justify-content: flex-end; }
.message.assistant { justify-content: flex-start; }

.message-bubble {
  max-width: 68%;
  padding: 10px 14px;
  border-radius: 14px;
  line-height: 1.55;
  font-size: 0.92rem;
}

.user-bubble {
  background: var(--accent);
  color: #fff;
  border-bottom-right-radius: 4px;
  border: none;
}

.user-bubble strong { color: rgba(255,255,255,0.75); }

.assistant-bubble {
  background: var(--bg-panel);
  color: var(--text-primary);
  border: 1px solid var(--bg-panel-border);
  border-bottom-left-radius: 4px;
  padding: 0;
  overflow: hidden;
}

.assistant-bubble:not(:has(.stock-card)):not(:has(.news-card)) { padding: 10px 14px; }

.message-bubble strong {
  display: block;
  font-size: 0.72rem;
  margin-bottom: 3px;
  opacity: 0.65;
  letter-spacing: 0.02em;
}

.message-bubble p { margin: 0; }

/* Extra breathing room before each user turn to separate Q&A pairs */
.message.user { margin-top: 10px; }

.ai-text {
  white-space: pre-wrap;
  font-size: 0.92rem;
  line-height: 1.6;
}

.price-bubble pre {
  margin: 4px 0 0 0;
  font-family: inherit;
  font-size: 0.9rem;
  white-space: pre-wrap;
  line-height: 1.8;
}

.loading {
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 6px;
  padding: 10px 14px !important;
  font-size: 0.88rem;
  color: var(--text-muted);
}

.dots { animation: blink 1.2s infinite; }
@keyframes blink { 0%, 100% { opacity: 1; } 50% { opacity: 0; } }

.loading-hint { font-size: 0.75rem; color: var(--text-muted); font-style: italic; }

.error-banner {
  background: rgba(233, 69, 96, 0.12);
  color: #ff6b6b;
  padding: 8px 16px;
  font-size: 0.85rem;
  border-top: 3px solid var(--accent);
}

.input-area {
  display: flex;
  padding: 14px;
  border-top: 1px solid var(--bg-panel-border);
  background: var(--bg-chat);
  gap: 10px;
  transition: background 0.3s, border-color 0.3s;
}

.input-area input {
  flex: 1;
  padding: 10px 14px;
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  font-size: 0.95rem;
  outline: none;
  transition: border-color 0.2s;
  background: var(--bg-panel-item);
  color: var(--text-chat);
}

.input-area input::placeholder { color: var(--text-muted); }
.input-area input:focus { border-color: var(--accent); }
.input-area input:disabled { background: var(--bg-chat-area); opacity: 0.6; }

.input-area button {
  padding: 10px 20px;
  background: var(--bg-panel);
  color: var(--text-primary);
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 0.95rem;
  transition: background 0.2s;
  min-width: 70px;
}

.input-area button:hover:not(:disabled) { background: var(--accent); }
.input-area button:disabled { background: var(--text-muted); cursor: not-allowed; }
</style>