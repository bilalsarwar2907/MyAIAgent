<template>
  <div class="convo-sidebar">

    <div class="convo-header">
      <span>💬 Chat History</span>
      <button class="new-btn" @click="startNew" title="New chat">+ New</button>
    </div>

    <div v-if="conversationStore.isLoading" class="convo-loading">Loading...</div>

    <div v-else-if="conversationStore.conversations.length === 0" class="convo-empty">
      <p>No past conversations.</p>
      <p>Start chatting to build history!</p>
    </div>

    <div v-else class="convo-list">
      <div
        v-for="convo in conversationStore.conversations"
        :key="convo.conversationId"
        class="convo-item"
        :class="{ active: convo.conversationId === chatStore.conversationId }"
        @click="loadConversation(convo.conversationId)"
      >
        <div class="convo-preview">
          {{ conversationStore.getPreview(convo.lastMessage) }}
        </div>
        <div class="convo-meta">
          <span class="convo-time">{{ conversationStore.getRelativeTime(convo.lastUpdated) }}</span>
          <span class="convo-count">{{ convo.messageCount }} msgs</span>
          <button
            class="convo-delete"
            @click.stop="deleteConversation(convo.conversationId)"
            title="Delete"
          >
            🗑️
          </button>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { onMounted } from 'vue';
import { useConversationStore } from '@/stores/conversationStore';
import { useChatStore } from '@/stores/chatStore';
import { useAuthStore } from '@/stores/authStore';
import api from '@/services/api';

const conversationStore = useConversationStore();
const chatStore = useChatStore();
const authStore = useAuthStore();

onMounted(async () => {
  if (authStore.userName) {
    await conversationStore.loadConversations(authStore.userName);
  }
});

const loadConversation = async (conversationId) => {
  try {
    const response = await api.get(
      '/conversations/' + authStore.userName + '/' + conversationId
    );

    const messages = response.data ?? [];

    chatStore.conversationId = conversationId;
    chatStore.messages = messages.map(m => ({
      role: m.role,
      content: m.content,
      type: m.type || 'text',
    }));

    chatStore.saveMessages();
    localStorage.setItem('conversationId', conversationId);

  } catch (err) {
    console.error('[ConversationSidebar] loadConversation error:', err);
  }
};

const startNew = () => {
  chatStore.newConversation();
};

const deleteConversation = async (conversationId) => {
  await conversationStore.deleteConversation(authStore.userName, conversationId);

  if (conversationId === chatStore.conversationId) {
    chatStore.newConversation();
  }
};
</script>

<style scoped>
.convo-sidebar {
  width: 100%;
  background: var(--bg-panel);
  color: var(--text-primary);
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  transition: background 0.3s, color 0.3s;
}

.convo-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  background: var(--bg-panel-header);
  font-weight: 700;
  font-size: 0.9rem;
  border-bottom: 1px solid var(--bg-panel-border);
}

.new-btn {
  background: var(--accent);
  color: white;
  border: none;
  padding: 4px 10px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 600;
}
.new-btn:hover { background: var(--accent-hover); }

.convo-loading, .convo-empty {
  padding: 20px;
  text-align: center;
  color: var(--text-muted);
  font-size: 0.82rem;
}

.convo-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.convo-item {
  background: var(--bg-panel-item);
  border: 1px solid var(--bg-panel-border);
  border-radius: 8px;
  padding: 10px 12px;
  cursor: pointer;
  transition: background 0.15s;
}

.convo-item:hover { background: var(--bg-panel-header); }

.convo-item.active {
  border-color: var(--accent);
  background: rgba(233, 69, 96, 0.12);
}

.convo-preview {
  font-size: 0.8rem;
  color: var(--text-primary);
  margin-bottom: 6px;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.convo-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.68rem;
  color: var(--text-muted);
}

.convo-time { flex: 1; }
.convo-count { margin-right: 6px; }

.convo-delete {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 0.75rem;
  opacity: 0.6;
  padding: 2px 4px;
  transition: opacity 0.15s;
}
.convo-delete:hover { opacity: 1; }
</style>