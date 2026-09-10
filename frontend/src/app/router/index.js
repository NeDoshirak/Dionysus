import { createRouter, createWebHistory } from 'vue-router'
import { restoreSession } from '@/entities/session'
import { AuthPage, LandingPage, ProfilePage, ProjectsPage, SpecificationPage } from '@/pages'

const routes = [
  { path: '/', name: 'landing', component: LandingPage },
  { path: '/sign-up', name: 'sign-up', component: AuthPage },
  { path: '/sign-in', name: 'sign-in', component: AuthPage },
  { path: '/verify-email', name: 'verify-email', component: AuthPage },
  { path: '/confirm-email/success', name: 'confirmation-success', component: AuthPage },
  { path: '/reset-password', name: 'reset-password', component: AuthPage },
  { path: '/projects', name: 'projects', component: ProjectsPage, meta: { requiresAuth: true } },
  { path: '/projects/:id/specification', name: 'specification', component: SpecificationPage, meta: { requiresAuth: true } },
  { path: '/profile', name: 'profile', component: ProfilePage, meta: { requiresAuth: true } },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth || await restoreSession()) return true
  return { name: 'sign-in' }
})

export default router
