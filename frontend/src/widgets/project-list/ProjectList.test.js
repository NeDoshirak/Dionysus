import { flushPromises, mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import { ProjectList } from './index'

const specificationApi = vi.hoisted(() => ({
  exportSpecificationMarkdown: vi.fn(),
}))

vi.mock('@/entities/specification', () => specificationApi)

it('shows loading state while projects are loading', () => {
  const wrapper = mount(ProjectList, { props: { loading: true, projects: [] } })
  expect(wrapper.text()).toContain('Загрузка проектов')
})

it('renders the empty state with a file tile and subtitle', () => {
  const wrapper = mount(ProjectList, { props: { projects: [], loading: false } })

  expect(wrapper.get('.project-list__empty .project-list__file').exists()).toBe(true)
  expect(wrapper.get('.project-list__empty strong').text()).toBe('Проектов пока нет')
  expect(wrapper.get('.project-list__empty small').text()).toBe('Создайте проект, чтобы начать')
})

it('navigates from a project row to its specification', async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/projects/:id/specification', name: 'specification', component: { template: '<p />' } }],
  })
  await router.push('/')
  await router.isReady()
  const wrapper = mount(ProjectList, {
    props: { projects: [{ id: 'project-1', name: 'Discovery', createdAt: '2026-09-10T08:30:00.000Z', status: 'completed' }] },
    global: { plugins: [router] },
  })
  await wrapper.get('.project-list__main').trigger('click')
  await flushPromises()
  expect(router.currentRoute.value.name).toBe('specification')
  expect(router.currentRoute.value.params.id).toBe('project-1')
})

it('exports project specification from the row action', async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/projects/:id/specification', name: 'specification', component: { template: '<p />' } }],
  })
  await router.push('/')
  await router.isReady()
  const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:specification')
  const revokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
  specificationApi.exportSpecificationMarkdown.mockResolvedValue({
    blob: new Blob(['# Specification'], { type: 'text/markdown' }),
    fileName: 'discovery-specification.md',
  })

  const wrapper = mount(ProjectList, {
    props: { projects: [{ id: 'project-1', name: 'Discovery', createdAt: '2026-09-10T08:30:00.000Z', status: 'completed' }] },
    global: { plugins: [router] },
  })
  const click = vi.fn()
  const link = { click }
  const createElement = vi.spyOn(document, 'createElement').mockReturnValue(link)

  await wrapper.get('.project-list__export').trigger('click')
  await flushPromises()

  expect(specificationApi.exportSpecificationMarkdown).toHaveBeenCalledWith('project-1')
  expect(link.href).toBe('blob:specification')
  expect(link.download).toBe('discovery-specification.md')
  expect(click).toHaveBeenCalled()
  expect(createObjectURL).toHaveBeenCalled()
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:specification')

  createElement.mockRestore()
  createObjectURL.mockRestore()
  revokeObjectURL.mockRestore()
})
