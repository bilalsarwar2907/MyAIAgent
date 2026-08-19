import { defineStore } from 'pinia';
import api from '@/services/api';
import { useAuthStore } from '@/stores/authStore';
import { useConversationStore } from '@/stores/conversationStore';

export const useChatStore = defineStore('chat', {
  state: () => ({
    conversationId: localStorage.getItem('conversationId') || null,
    messages: (() => { try { return JSON.parse(localStorage.getItem('messages') || '[]'); } catch { return []; } })(),
    isLoading: false,
    isAnalyzing: false,
    error: null as string | null,
  }),

  actions: {
    initConversation() {
      if (!this.conversationId) {
        this.conversationId = crypto.randomUUID();
        localStorage.setItem('conversationId', this.conversationId);
      }
    },

    newConversation() {
      this.conversationId = crypto.randomUUID();
      this.messages = [];
      this.error = null;
      localStorage.setItem('conversationId', this.conversationId);
      localStorage.setItem('messages', JSON.stringify([]));
    },

    saveMessages() {
      localStorage.setItem('messages', JSON.stringify(this.messages));
    },

    async sendMessage(userMessage: string) {
      if (!userMessage.trim()) return;

      this.isLoading = true;
      this.isAnalyzing = false;
      this.error = null;

      // Add user message immediately
      this.messages.push({ role: 'user', content: userMessage, type: 'text' });
      this.saveMessages();

      // Detect if this is an analysis query so we can show the right hint
      const lower = userMessage.toLowerCase();
      const isAnalysis =
        lower.includes('analyze') ||
        lower.includes('analyse') ||
        lower.includes('compare') ||
        lower.includes('should i buy') ||
        lower.includes('should i sell') ||
        lower.includes('vs') ||
        lower.includes('versus') ||
        lower.includes('recommend');

      if (isAnalysis) this.isAnalyzing = true;

      try {
        const authStore = useAuthStore();
        const response = await api.post('/chat', {
          message: userMessage,
          conversationId: this.conversationId,
          userName: authStore.userName,
        });

        const data = response.data;

        if (data.toolUsed && data.tool === 'AnalyzeStock') {
          // ✅ Stock analysis — render as StockCard
          this.messages.push({
            role: 'assistant',
            content: data.result || '',
            type: 'analysis',
            symbols: data.symbols || '',
          });

        } else if (data.toolUsed && data.tool === 'GetStockNews') {
          this.messages.push({
            role: 'assistant',
            content: data.result || '',
            type: 'news',
            symbols: data.symbols || '',
          });

        } else if (data.toolUsed && data.tool === 'GetStockPrice') {
          // ✅ Stock price — render as price bubble
          this.messages.push({
            role: 'assistant',
            content: data.result || '',
            type: 'price',
          });

        } else if (data.toolUsed) {
          // ✅ Other tool (e.g. SaveNote)
          this.messages.push({
            role: 'assistant',
            content: '✅ ' + data.tool + ': ' + (data.result || ''),
            type: 'text',
          });

        } else {
          // ✅ Normal AI reply
          this.messages.push({
            role: 'assistant',
            content: data.message || '',
            type: 'text',
          });
        }

        this.saveMessages();

        // Refresh the conversation history sidebar with the latest data
        const conversationStore = useConversationStore();
        if (authStore.userName) {
          conversationStore.loadConversations(authStore.userName);
        }

      } catch (err: any) {
        this.error = 'Could not reach the server. Is the backend running?';
        this.messages.push({
          role: 'assistant',
          content: 'Sorry, an error occurred. Please try again.',
          type: 'text',
        });
        this.saveMessages();
        console.error('[chatStore] sendMessage error:', err);
      } finally {
        this.isLoading = false;
        this.isAnalyzing = false;
      }
    },
  },
});