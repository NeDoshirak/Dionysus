import { afterEach, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { clearSession, setSession } from '@/entities/session'
import AppHeader from './AppHeader.vue'

afterEach(() => {
  clearSession()
})

it('shows the authenticated account email instead of a fictional profile name', () => {
  setSession({ accessToken: 'token', email: 'person@example.com' })
  const wrapper = mount(AppHeader, {
    global: { stubs: { RouterLink: { template: '<a><slot /></a>' } } },
  })

  expect(wrapper.get('.app-header__profile').text()).toContain('person@example.com')
  expect(wrapper.get('.app-header__profile').text()).not.toContain('Александр И.')
  expect(wrapper.get('.app-header__avatar').text()).toBe('P')
})
