import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, it, vi } from 'vitest'
import SignOutButton from './SignOutButton.vue'
import { getSession, setSession } from '@/entities/session'

it('clears the local session and routes to the landing page', async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'landing', component: { template: '<div />' } },
      { path: '/profile', name: 'profile', component: { template: '<div />' } },
    ],
  })
  setSession({ accessToken: 'token' })
  await router.push('/profile')
  await router.isReady()
  const wrapper = mount(SignOutButton, { global: { plugins: [router] } })

  await wrapper.get('button').trigger('click')
  await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('landing'))

  expect(getSession()).toBeNull()
  expect(router.currentRoute.value.name).toBe('landing')
})
