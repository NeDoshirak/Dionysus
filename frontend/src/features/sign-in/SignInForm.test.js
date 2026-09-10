import { mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import SignInForm from './SignInForm.vue'

const push = vi.fn()
const sessionMocks = vi.hoisted(() => ({ signIn: vi.fn() }))

vi.mock('@/entities/session', () => sessionMocks)

vi.mock('vue-router', async () => {
  const actual = await vi.importActual('vue-router')
  return { ...actual, useRouter: () => ({ push }) }
})

beforeEach(() => {
  push.mockReset()
  sessionMocks.signIn.mockReset()
})

it('shows an invalid credentials message', async () => {
  sessionMocks.signIn.mockRejectedValue({ status: 401 })
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('unknown@example.com')
  await wrapper.get('input[type="password"]').setValue('WrongPass1')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Неверный email или пароль.')
})

it('uses the Figma sign-in heading and supporting copy', () => {
  const wrapper = mount(SignInForm)

  expect(wrapper.get('h1').text()).toBe('Войти в аккаунт')
  expect(wrapper.get('.auth-form-frame__subtitle').text()).toBe('Продолжите работу над вашими встречами.')
})

it('routes unconfirmed users to email verification with their email', async () => {
  sessionMocks.signIn.mockRejectedValue({ code: 'unconfirmed-email', status: 403 })
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('unconfirmed@dionysus.app')
  await wrapper.get('input[type="password"]').setValue('Demo1234')
  await wrapper.get('form').trigger('submit')

  expect(push).toHaveBeenCalledWith({ name: 'verify-email', query: { email: 'unconfirmed@dionysus.app' } })
})

it('shows email validation feedback without calling the sign-in API', async () => {
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('invalid-email')
  await wrapper.get('input[type="password"]').setValue('Demo1234')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Введите корректный email.')
  expect(wrapper.text()).not.toContain('Неверный email или пароль.')
  expect(sessionMocks.signIn).not.toHaveBeenCalled()
})

it('submits valid credentials through the session API', async () => {
  sessionMocks.signIn.mockResolvedValue({ accessToken: 'token', expiresAt: '2026-09-11T12:00:00.000Z' })
  const wrapper = mount(SignInForm)

  await wrapper.get('input[type="email"]').setValue('person@example.com')
  await wrapper.get('input[type="password"]').setValue('Password1')
  await wrapper.get('form').trigger('submit')

  expect(sessionMocks.signIn).toHaveBeenCalledWith({ email: 'person@example.com', password: 'Password1' })
  expect(push).toHaveBeenCalledWith({ name: 'projects' })
})
