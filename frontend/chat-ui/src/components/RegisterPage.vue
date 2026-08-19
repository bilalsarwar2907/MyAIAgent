<template>
  <div class="auth-wrapper">
    <div class="auth-card">

      <!-- Logo / Title -->
      <div class="auth-header">
        <h1>🤖 AI Agent</h1>
        <p>Create your account to get started</p>
      </div>

      <!-- Error Message -->
      <div v-if="authStore.error" class="error-box">
        ⚠️ {{ authStore.error }}
      </div>

      <!-- Success Message -->
      <div v-if="successMessage" class="success-box">
        ✅ {{ successMessage }}
      </div>

      <!-- Register Form -->
      <form @submit.prevent="handleRegister">
        <div class="form-group">
          <label>Username</label>
          <input
            v-model="userName"
            type="text"
            placeholder="Choose a username"
            :disabled="authStore.isLoading"
            required
          />
        </div>

        <div class="form-group">
          <label>Password</label>
          <input
            v-model="password"
            type="password"
            placeholder="Choose a password"
            :disabled="authStore.isLoading"
            required
          />
        </div>

        <div class="form-group">
          <label>Confirm Password</label>
          <input
            v-model="confirmPassword"
            type="password"
            placeholder="Repeat your password"
            :disabled="authStore.isLoading"
            required
          />
        </div>

        <!-- Password mismatch warning -->
        <div v-if="passwordMismatch" class="error-box">
          ⚠️ Passwords do not match.
        </div>

        <button type="submit" class="btn-primary" :disabled="authStore.isLoading || passwordMismatch">
          {{ authStore.isLoading ? 'Creating account...' : 'Register' }}
        </button>
      </form>

      <!-- Link to Login -->
      <p class="switch-link">
        Already have an account?
        <router-link to="/login">Login here</router-link>
      </p>

    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/authStore';

const authStore = useAuthStore();
const router = useRouter();

const userName = ref('');
const password = ref('');
const confirmPassword = ref('');
const successMessage = ref('');

// Computed — true if passwords don't match (only show after user types in confirm field)
const passwordMismatch = computed(() => {
  return confirmPassword.value.length > 0 && password.value !== confirmPassword.value;
});

const handleRegister = async () => {
  if (passwordMismatch.value) return;

  await authStore.register(userName.value, password.value);

  // If registered and logged in successfully, go to chat
  if (authStore.isLoggedIn) {
    router.push('/');
  }
};
</script>

<style scoped>
.auth-wrapper {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #1a1a2e;
}

.auth-card {
  background: white;
  border-radius: 16px;
  padding: 40px;
  width: 100%;
  max-width: 420px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
}

.auth-header {
  text-align: center;
  margin-bottom: 30px;
}

.auth-header h1 {
  font-size: 2rem;
  margin: 0 0 8px 0;
  color: #1a1a2e;
}

.auth-header p {
  color: #888;
  margin: 0;
  font-size: 0.9rem;
}

.error-box {
  background: #fff3cd;
  color: #856404;
  border: 1px solid #ffc107;
  border-radius: 8px;
  padding: 10px 14px;
  margin-bottom: 16px;
  font-size: 0.9rem;
}

.success-box {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
  border-radius: 8px;
  padding: 10px 14px;
  margin-bottom: 16px;
  font-size: 0.9rem;
}

.form-group {
  margin-bottom: 18px;
}

.form-group label {
  display: block;
  margin-bottom: 6px;
  font-weight: 600;
  color: #333;
  font-size: 0.9rem;
}

.form-group input {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #ccc;
  border-radius: 8px;
  font-size: 0.95rem;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
}

.form-group input:focus {
  border-color: #1a1a2e;
}

.form-group input:disabled {
  background: #f5f5f5;
}

.btn-primary {
  width: 100%;
  padding: 12px;
  background: #1a1a2e;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s;
  margin-top: 6px;
}

.btn-primary:hover:not(:disabled) {
  background: #e94560;
}

.btn-primary:disabled {
  background: #aaa;
  cursor: not-allowed;
}

.switch-link {
  text-align: center;
  margin-top: 20px;
  color: #666;
  font-size: 0.9rem;
}

.switch-link a {
  color: #e94560;
  text-decoration: none;
  font-weight: 600;
}

.switch-link a:hover {
  text-decoration: underline;
}
</style>