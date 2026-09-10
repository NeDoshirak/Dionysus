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

  expect(wrapper.text()).toContain('person@example.com')
})
