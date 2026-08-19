import { defineStore } from 'pinia';
import api from '@/services/api';

interface ConversationSummary {
  conversationId: string;
  lastMessage: string;
  lastUpdated: string;
  messageCount: number;
}

export const useConversationStore = defineStore('conversation', {
  state: () => ({
    conversations: [] as ConversationSummary[],
    isLoading: false,
    error: null as string | null,
  }),

  actions: {
    // =====================
    // LOAD all conversations for a user
    // =====================
    async loadConversations(userName: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        const response = await api.get('/conversations/' + userName);
        this.conversations = response.data ?? [];
      } catch (err: unknown) {
        this.error = 'Failed to load conversation history.';
        console.error('[conversationStore] loadConversations error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // DELETE a conversation
    // =====================
    async deleteConversation(userName: string, conversationId: string): Promise<void> {
      this.error = null;

      try {
        await api.delete('/conversations/' + userName + '/' + conversationId);
        this.conversations = this.conversations.filter(
          c => c.conversationId !== conversationId
        );
      } catch (err: unknown) {
        this.error = 'Failed to delete conversation.';
        console.error('[conversationStore] deleteConversation error:', err);
      }
    },

    // =====================
    // Format a preview string for the sidebar
    // =====================
    getPreview(message: string, maxLength: number = 40): string {
      if (!message) return 'New conversation';
      return message.length > maxLength
        ? message.substring(0, maxLength) + '...'
        : message;
    },

    // =====================
    // Format relative time e.g. "2h ago"
    // =====================
    getRelativeTime(dateString: string): string {
      const date = new Date(dateString);
      if (isNaN(date.getTime())) return '';
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMins = Math.floor(diffMs / 60000);
      const diffHours = Math.floor(diffMins / 60);
      const diffDays = Math.floor(diffHours / 24);

      if (diffMins < 1) return 'just now';
      if (diffMins < 60) return diffMins + 'm ago';
      if (diffHours < 24) return diffHours + 'h ago';
      if (diffDays < 7) return diffDays + 'd ago';
      return date.toLocaleDateString();
    },
  },
});
