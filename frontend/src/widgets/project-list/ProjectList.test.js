import { flushPromises, mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import ProjectList from './ProjectList.vue'

it('shows loading state while projects are loading', () => {
  const wrapper = mount(ProjectList, { props: { loading: true, projects: [] } })
  expect(wrapper.text()).toContain('Загрузка проектов')
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
  await wrapper.get('.project-list__row').trigger('click')
  await flushPromises()
  expect(router.currentRoute.value.name).toBe('specification')
  expect(router.currentRoute.value.params.id).toBe('project-1')
})
