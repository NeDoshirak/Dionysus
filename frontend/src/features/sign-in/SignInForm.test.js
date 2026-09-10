import { mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import SignInForm from './SignInForm.vue'

const push = vi.fn()

vi.mock('vue-router', async () => {
  const actual = await vi.importActual('vue-router')
  return { ...actual, useRouter: () => ({ push }) }
})

it('shows an invalid credentials message', async () => {
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('unknown@example.com')
  await wrapper.get('input[type="password"]').setValue('WrongPass1')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Неверный email или пароль.')
})

it('routes unconfirmed users to email verification with their email', async () => {
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('unconfirmed@dionysus.app')
  await wrapper.get('input[type="password"]').setValue('Demo1234')
  await wrapper.get('form').trigger('submit')

  expect(push).toHaveBeenCalledWith({ name: 'verify-email', query: { email: 'unconfirmed@dionysus.app' } })
})

it('shows email validation feedback without calling the sign-in adapter', async () => {
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('invalid-email')
  await wrapper.get('input[type="password"]').setValue('Demo1234')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Введите корректный email.')
  expect(wrapper.text()).not.toContain('Неверный email или пароль.')
})
