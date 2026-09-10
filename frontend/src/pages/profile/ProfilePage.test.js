import { afterEach, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { clearSession, setSession } from '@/entities/session'
import ProfilePage from './ProfilePage.vue'

afterEach(() => {
  clearSession()
})

it('shows the authenticated account email without a fictional user name', () => {
  setSession({ accessToken: 'token', email: 'person@example.com' })
  const wrapper = mount(ProfilePage, {
    global: {
      stubs: {
        AppHeader: true,
        ProfileForm: true,
        RouterLink: true,
        SignOutButton: true,
      },
    },
  })

  expect(wrapper.get('h1').text()).toBe('Профиль')
  expect(wrapper.text()).toContain('person@example.com')
  expect(wrapper.text()).not.toContain('Александр Иванов')
  expect(wrapper.text()).not.toContain('alex@company.com')
  expect(wrapper.get('.profile-page__avatar').text()).toBe('P')
})
