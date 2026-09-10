import { createRouter, createWebHistory } from 'vue-router'
import { LandingPage } from '@/pages/landing'
import { AuthPage } from '@/pages/auth'

const routes = [
  { path: '/', name: 'landing', component: LandingPage },
  { path: '/sign-up', name: 'sign-up', component: AuthPage },
  { path: '/sign-in', name: 'sign-in', component: AuthPage },
  { path: '/verify-email', name: 'verify-email', component: AuthPage },
  { path: '/confirm-email/success', name: 'confirmation-success', component: AuthPage },
  { path: '/reset-password', name: 'reset-password', component: AuthPage },
  { path: '/projects', name: 'projects' },
  { path: '/projects/:id/specification', name: 'specification' },
  { path: '/profile', name: 'profile' },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

export default router
