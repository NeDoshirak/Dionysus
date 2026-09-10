import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import SignUpForm from './SignUpForm.vue'

it('rejects a password that does not meet the account password policy', async () => {
  const wrapper = mount(SignUpForm)

  await wrapper.get('input[type="email"]').setValue('person@example.com')
  await wrapper.get('input[type="password"]').setValue('password')
  await wrapper.get('input[type="checkbox"]').setValue(true)
  await wrapper.get('form').trigger('submit')

  expect(wrapper.text()).toContain('Пароль должен содержать не менее 8 символов, заглавную букву и цифру.')
  expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBeUndefined()
})

it('groups account fields after the form header for shared spacing', () => {
  const wrapper = mount(SignUpForm)

  expect(wrapper.get('.sign-up-form__fields').findAll('.base-input')).toHaveLength(2)
})
