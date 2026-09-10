import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import BaseInput from './BaseInput.vue'
import BaseButton from './BaseButton.vue'
import BaseDialog from './BaseDialog.vue'
import StatusMessage from './StatusMessage.vue'

it('connects an input to its label and error and emits entered values', async () => {
  const wrapper = mount(BaseInput, { props: { label: 'Email', type: 'email', modelValue: '', error: 'Введите email' } })
  const input = wrapper.get('input')
  expect(wrapper.get('label').attributes('for')).toBe(input.attributes('id'))
  expect(input.attributes('aria-invalid')).toBe('true')
  expect(wrapper.get(`#${input.attributes('aria-describedby')}`).text()).toBe('Введите email')
  await input.setValue('analyst@example.com')
  expect(wrapper.emitted('update:modelValue')).toEqual([['analyst@example.com']])
})

it('uses the compact Figma control sizing', () => {
  const wrapper = mount(BaseInput, { props: { label: 'Email' } })

  expect(wrapper.get('input').classes()).toContain('base-input__control')
})

it('gives each input a unique label association and forwards native attributes', () => {
  const wrapper = mount({ components: { BaseInput }, template: '<div><BaseInput label="Email" autocomplete="email" required /><BaseInput label="Password" type="password" /></div>' })
  const inputs = wrapper.findAll('input')
  expect(inputs[0].attributes('id')).not.toBe(inputs[1].attributes('id'))
  expect(inputs[0].attributes('autocomplete')).toBe('email')
  expect(inputs[0].attributes('required')).toBeDefined()
})

it('renders a trailing control within the input field', () => {
  const wrapper = mount(BaseInput, {
    props: { label: 'Пароль', type: 'password' },
    slots: { trailing: '<button class="password-toggle" type="button">Показать</button>' },
  })

  expect(wrapper.get('.base-input__trailing .password-toggle').text()).toBe('Показать')
})

it('applies consumer layout classes to the field root instead of the native control', () => {
  const wrapper = mount(BaseInput, { props: { label: 'Email' }, attrs: { class: 'form__field' } })

  expect(wrapper.classes()).toContain('form__field')
  expect(wrapper.get('input').classes()).not.toContain('form__field')
})

it('prevents duplicate actions while a button is loading', async () => {
  const wrapper = mount(BaseButton, { props: { loading: true }, slots: { default: 'Сохранить' } })
  expect(wrapper.get('button').attributes('disabled')).toBeDefined()
  expect(wrapper.get('button').attributes('aria-busy')).toBe('true')
  await wrapper.get('button').trigger('click')
  expect(wrapper.emitted('click')).toBeUndefined()
})

it('announces errors urgently and loading updates politely', async () => {
  const wrapper = mount(StatusMessage, { props: { state: 'error' }, slots: { default: 'Не удалось загрузить' } })
  expect(wrapper.attributes('role')).toBe('alert')
  await wrapper.setProps({ state: 'loading' })
  expect(wrapper.attributes('role')).toBe('status')
  expect(wrapper.attributes('aria-busy')).toBe('true')
})

it('labels the dialog and requests closure through Escape and its close button', async () => {
  const wrapper = mount(BaseDialog, { props: { title: 'Новый проект' } })
  const dialog = wrapper.get('dialog')
  expect(dialog.attributes('aria-labelledby')).toBeDefined()
  expect(wrapper.get(`#${dialog.attributes('aria-labelledby')}`).text()).toBe('Новый проект')
  await dialog.trigger('cancel')
  await wrapper.get('button[aria-label="Закрыть"]').trigger('click')
  expect(wrapper.emitted('close')).toHaveLength(2)
})

it('marks the dialog for the opening fade animation', () => {
  const wrapper = mount(BaseDialog, { props: { title: 'Новый проект' } })

  expect(wrapper.get('dialog').classes()).toContain('base-dialog--fade-in')
})
