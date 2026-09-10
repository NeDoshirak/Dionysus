import { mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import ResetPasswordForm from './ResetPasswordForm.vue'

const push = vi.fn()

vi.mock('vue-router', async () => {
  const actual = await vi.importActual('vue-router')
  return { ...actual, useRouter: () => ({ push }), useRoute: () => ({ query: {} }) }
})

it('starts a resend-code timer after requesting a reset code', async () => {
  vi.useFakeTimers()
  const wrapper = mount(ResetPasswordForm)

  await wrapper.get('input[type="email"]').setValue('person@example.com')
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Отправить повторно через 55 с')
  await vi.advanceTimersByTimeAsync(1000)
  expect(wrapper.text()).toContain('Отправить повторно через 54 с')
  vi.useRealTimers()
})

it('preserves the requested email in the code step', async () => {
  const wrapper = mount(ResetPasswordForm, { props: { email: 'person@example.com', step: 'code' } })

  expect(wrapper.text()).toContain('p***@example.com')
  expect(wrapper.text()).not.toContain('person@example.com')
  expect(wrapper.findAll('input[inputmode="numeric"]')).toHaveLength(6)
  expect(wrapper.text()).toContain('Изменить email')
})

it('renders six accessible code cells with one-digit constraints', () => {
  const wrapper = mount(ResetPasswordForm, { props: { email: 'person@example.com', step: 'code' } })
  const cells = wrapper.findAll('input[inputmode="numeric"]')

  expect(cells).toHaveLength(6)
  expect(cells.every((cell) => cell.attributes('maxlength') === '1')).toBe(true)
})
