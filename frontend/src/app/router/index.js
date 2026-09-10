import { createRouter, createWebHistory } from 'vue-router'
import { ProjectsPage } from '@/pages'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [{ path: '/projects', name: 'projects', component: ProjectsPage }],
})

export default router
