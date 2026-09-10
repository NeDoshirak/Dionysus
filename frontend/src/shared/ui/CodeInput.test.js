import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import CodeInput from './CodeInput.vue'

it('distributes pasted digits and emits the complete code', async () => {
  const wrapper = mount(CodeInput, { attachTo: document.body })
  const inputs = wrapper.findAll('input')

  await inputs[0].trigger('paste', {
    clipboardData: { getData: () => '1 2a3-456' },
  })

  expect(inputs.map((input) => input.element.value)).toEqual(['1', '2', '3', '4', '5', '6'])
  expect(wrapper.emitted('update:modelValue')).toContainEqual(['123456'])
  expect(document.activeElement).toBe(inputs[5].element)
})

it('moves focus backwards from an empty cell on Backspace', async () => {
  const wrapper = mount(CodeInput, { attachTo: document.body })
  const inputs = wrapper.findAll('input')

  inputs[1].element.focus()
  await inputs[1].trigger('keydown', { key: 'Backspace' })

  expect(document.activeElement).toBe(inputs[0].element)
})
