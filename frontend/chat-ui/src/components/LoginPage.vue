<template>
  <div class="auth-wrapper">
    <div class="auth-card">
      <div class="auth-header">
        <h1>🤖 AI Agent</h1>
        <p>Your personal learning & stock assistant</p>
      </div>
      <div v-if="authStore.error" class="error-box">
        ⚠️ {{ authStore.error }}
      </div>
      <form @submit.prevent="handleLogin">
        <div class="form-group">
          <label>Username</label>
          <input v-model="userName" type="text" placeholder="Enter your username" :disabled="authStore.isLoading" required />
        </div>
        <div class="form-group">
          <label>Password</label>
          <input v-model="password" type="password" placeholder="Enter your password" :disabled="authStore.isLoading" required />
        </div>
        <button type="submit" class="btn-primary" :disabled="authStore.isLoading">
          {{ authStore.isLoading ? 'Logging in...' : 'Login' }}
        </button>
      </form>
      <p class="switch-link">
        Don't have an account?
        <router-link to="/register">Register here</router-link>
      </p>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/authStore';

const authStore = useAuthStore();
const router = useRouter();
const userName = ref('');
const password = ref('');

const handleLogin = async () => {
  await authStore.login(userName.value, password.value);
  if (authStore.isLoggedIn) {
    router.push('/');
  }
};
</script>

<style scoped>
.auth-wrapper { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #1a1a2e; }
.auth-card { background: white; border-radius: 16px; padding: 40px; width: 100%; max-width: 420px; box-shadow: 0 8px 32px rgba(0,0,0,0.3); }
.auth-header { text-align: center; margin-bottom: 30px; }
.auth-header h1 { font-size: 2rem; margin: 0 0 8px 0; color: #1a1a2e; }
.auth-header p { color: #888; margin: 0; font-size: 0.9rem; }
.error-box { background: #fff3cd; color: #856404; border: 1px solid #ffc107; border-radius: 8px; padding: 10px 14px; margin-bottom: 20px; font-size: 0.9rem; }
.form-group { margin-bottom: 18px; }
.form-group label { display: block; margin-bottom: 6px; font-weight: 600; color: #333; font-size: 0.9rem; }
.form-group input { width: 100%; padding: 10px 14px; border: 1px solid #ccc; border-radius: 8px; font-size: 0.95rem; outline: none; box-sizing: border-box; }
.form-group input:focus { border-color: #1a1a2e; }
.form-group input:disabled { background: #f5f5f5; }
.btn-primary { width: 100%; padding: 12px; background: #1a1a2e; color: white; border: none; border-radius: 8px; font-size: 1rem; cursor: pointer; margin-top: 6px; }
.btn-primary:hover:not(:disabled) { background: #e94560; }
.btn-primary:disabled { background: #aaa; cursor: not-allowed; }
.switch-link { text-align: center; margin-top: 20px; color: #666; font-size: 0.9rem; }
.switch-link a { color: #e94560; text-decoration: none; font-weight: 600; }
</style>