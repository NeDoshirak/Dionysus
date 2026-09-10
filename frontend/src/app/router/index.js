import { createRouter, createWebHistory } from 'vue-router'
import { LandingPage } from '@/pages/landing'

const routes = [
  { path: '/', name: 'landing', component: LandingPage },
  { path: '/sign-up', name: 'sign-up' },
  { path: '/sign-in', name: 'sign-in' },
  { path: '/verify-email', name: 'verify-email' },
  { path: '/reset-password', name: 'reset-password' },
  { path: '/projects', name: 'projects' },
  { path: '/projects/:id/specification', name: 'specification' },
  { path: '/profile', name: 'profile' },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

export default router
