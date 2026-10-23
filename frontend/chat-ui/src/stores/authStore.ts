import { defineStore } from 'pinia';
import api from '@/services/api';
import { errorMessage, errorStatus } from '@/services/apiError';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    userName: localStorage.getItem('userName') || null as string | null,
    isLoading: false,
    error: null as string | null,
  }),

  getters: {
    isLoggedIn: (state): boolean => !!state.userName,
  },

  actions: {
    // =====================
    // REGISTER
    // =====================
    async register(userName: string, password: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        await api.post('/register', {
          userName: userName,
          password: password,
        });

        // Auto login after successful register
        await this.login(userName, password);

      } catch (err: unknown) {
        this.error = errorMessage(err, 'Registration failed. Please try again.');
        console.error('[authStore] register error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // LOGIN
    // =====================
    async login(userName: string, password: string): Promise<void> {
      this.isLoading = true;
      this.error = null;

      try {
        const response = await api.post('/login', {
          username: userName,
          password: password,
        });

        this.userName = response.data.userName as string;
        localStorage.setItem('userName', this.userName ?? '');

      } catch (err: unknown) {
        if (errorStatus(err) === 401) {
          this.error = 'Invalid username or password.';
        } else {
          this.error = errorMessage(err, 'Login failed. Is the backend running?');
        }
        console.error('[authStore] login error:', err);
      } finally {
        this.isLoading = false;
      }
    },

    // =====================
    // LOGOUT
    // =====================
    logout(): void {
      this.userName = null;
      this.error = null;
      localStorage.removeItem('userName');
      localStorage.removeItem('conversationId');
      localStorage.removeItem('messages');
    },
  },
});