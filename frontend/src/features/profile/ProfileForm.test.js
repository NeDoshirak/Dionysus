import { mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import ProfileForm from './ProfileForm.vue'

it('keeps profile changes local and shows a saved state', async () => {
  const wrapper = mount(ProfileForm, { props: { email: 'alex@company.com' } })

  await wrapper.get('input[type="email"]').setValue('new@company.com')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Изменения сохранены локально')
  expect(wrapper.get('input[type="email"]').element.value).toBe('new@company.com')
})

it('does not submit a fictional persistence request for password changes', async () => {
  const wrapper = mount(ProfileForm, { props: { email: 'alex@company.com' } })
  const fetchSpy = vi.spyOn(globalThis, 'fetch')

  await wrapper.get('input[autocomplete="current-password"]').setValue('Current123')
  await wrapper.get('input[autocomplete="new-password"]').setValue('NewPass123')
  await wrapper.get('input[autocomplete="new-password-confirmation"]').setValue('NewPass123')
  await wrapper.findAll('form')[1].trigger('submit')

  expect(fetchSpy).not.toHaveBeenCalled()
  fetchSpy.mockRestore()
})
