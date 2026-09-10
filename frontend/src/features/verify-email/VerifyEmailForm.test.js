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
