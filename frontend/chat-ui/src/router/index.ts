import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import ChatWindow from '@/components/ChatWindow.vue'
import LoginPage from '@/components/LoginPage.vue'
import RegisterPage from '@/components/RegisterPage.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'chat',
      component: ChatWindow,
      meta: { requiresAuth: true }, // protected - must be logged in
    },
    {
      path: '/login',
      name: 'login',
      component: LoginPage,
      meta: { requiresAuth: false },
    },
    {
      path: '/register',
      name: 'register',
      component: RegisterPage,
      meta: { requiresAuth: false },
    },
    {
      // Catch any unknown route and redirect to chat
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

// Navigation Guard — runs before every route change
// If route requires auth and user is not logged in → redirect to login
router.beforeEach((to) => {
  const authStore = useAuthStore()

  if (to.meta.requiresAuth && !authStore.isLoggedIn) {
    return { name: 'login' }
  }

  // If user is already logged in and tries to go to login/register → redirect to chat
  if (!to.meta.requiresAuth && authStore.isLoggedIn) {
    return { name: 'chat' }
  }
})

export default router