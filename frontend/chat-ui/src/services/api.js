import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:60363';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 300000, // 5 minutes — analysis fetches multiple APIs then runs AI
});

api.interceptors.request.use(
  (config) => {
    if (import.meta.env.DEV) console.log('[API] Request:', config.method.toUpperCase(), config.url);
    return config;
  },
  (error) => {
    console.error('[API] Request error:', error);
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.code === 'ECONNABORTED') {
      console.error('[API] Request timed out — is Ollama running?');
    } else if (!error.response) {
      console.error('[API] No response from server — is the backend running?');
    } else {
      console.error('[API] Response error:', error.response.status, error.response.data);
    }
    return Promise.reject(error);
  }
);

export default api;