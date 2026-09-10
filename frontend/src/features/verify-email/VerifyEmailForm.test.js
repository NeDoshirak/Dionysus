import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import VerifyEmailForm from './VerifyEmailForm.vue'

it('requires all six code digits before verification can be submitted', async () => {
  const wrapper = mount(VerifyEmailForm, { props: { email: 'person@example.com' } })

  const inputs = wrapper.findAll('input[inputmode="numeric"]')
  for (const [index, input] of inputs.slice(0, 5).entries()) {
    await input.setValue(String(index + 1))
  }
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Введите все 6 цифр кода.')
  expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBeUndefined()
})

it('masks the displayed verification email while retaining the email prop', () => {
  const wrapper = mount(VerifyEmailForm, { props: { email: 'person@example.com' } })

  expect(wrapper.get('.verify-email-form__email').text()).toContain('p***@example.com')
  expect(wrapper.get('.verify-email-form__email').text()).not.toContain('person@example.com')
  expect(wrapper.props('email')).toBe('person@example.com')
})

it('moves OTP focus forward after entering a digit', async () => {
  const wrapper = mount(VerifyEmailForm, { attachTo: document.body })
  const inputs = wrapper.findAll('input[inputmode="numeric"]')

  await inputs[0].setValue('1')

  expect(document.activeElement).toBe(inputs[1].element)
})

it('moves OTP focus backward from an empty cell on Backspace', async () => {
  const wrapper = mount(VerifyEmailForm, { attachTo: document.body })
  const inputs = wrapper.findAll('input[inputmode="numeric"]')

  inputs[1].element.focus()
  await inputs[1].trigger('keydown', { key: 'Backspace' })

  expect(document.activeElement).toBe(inputs[0].element)
})

it('distributes six pasted OTP digits across the cells', async () => {
  const wrapper = mount(VerifyEmailForm, { attachTo: document.body })
  const inputs = wrapper.findAll('input[inputmode="numeric"]')

  await inputs[0].trigger('paste', {
    clipboardData: { getData: () => '1 2a3-456' },
  })

  expect(inputs.map((input) => input.element.value)).toEqual(['1', '2', '3', '4', '5', '6'])
  expect(document.activeElement).toBe(inputs[5].element)
})
