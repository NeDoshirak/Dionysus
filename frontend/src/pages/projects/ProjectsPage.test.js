import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, expect, it, vi } from 'vitest'
import ProjectsPage from './ProjectsPage.vue'

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))
vi.mock('@/entities/project', async (importOriginal) => ({
  ...(await importOriginal()),
  getProjects: vi.fn().mockResolvedValue([]),
}))

afterEach(() => {
  vi.unstubAllEnvs()
})

it('uses the compact icon-only create-project action from the design', async () => {
  const wrapper = mount(ProjectsPage, {
    global: {
      stubs: {
        AppHeader: true,
        ProjectList: true,
        ProjectSearch: true,
        CreateProjectDialog: true,
      },
    },
  })
  await flushPromises()

  const button = wrapper.get('button[aria-label="Новый проект"]')
  expect(button.classes()).toContain('projects-page__create-button')
  expect(button.get('img').attributes('src')).toContain('data:image/svg+xml')
  expect(button.text()).toContain('Новый проект')
})

it('hides project creation in the read-only demo preview', async () => {
  vi.stubEnv('VITE_DEMO_MODE', 'true')
  const wrapper = mount(ProjectsPage, {
    global: {
      stubs: { AppHeader: true, ProjectList: true, ProjectSearch: true, CreateProjectDialog: true },
    },
  })

  await flushPromises()

  expect(wrapper.find('button[aria-label="Новый проект"]').exists()).toBe(false)
})
