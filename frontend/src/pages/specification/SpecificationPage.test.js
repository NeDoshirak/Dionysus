import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, it, vi } from 'vitest'
import SpecificationPage from './SpecificationPage.vue'

it('shows a project-aware specification placeholder without editor controls', async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/projects/:id/specification', name: 'specification', component: SpecificationPage }, { path: '/projects', name: 'projects', component: { template: '<div />' } }],
  })
  await router.push({ name: 'specification', params: { id: 'project-42' }, query: { name: 'Discovery' } })
  await router.isReady()
  const wrapper = mount(SpecificationPage, { global: { plugins: [router] } })

  expect(wrapper.text()).toContain('Discovery')
  expect(wrapper.text()).toContain('Страница ТЗ готовится')
  expect(wrapper.text()).not.toContain('Транскрипция')
  expect(wrapper.find('[contenteditable="true"]').exists()).toBe(false)
  await wrapper.get('.specification-page__back').trigger('click')
  await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('projects'))
})
