import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import AuthFormFrame from './AuthFormFrame.vue'

it('provides a consistent semantic frame for authentication forms', async () => {
  const wrapper = mount(AuthFormFrame, {
    props: { title: 'Войти в аккаунт', subtitle: 'Продолжите работу.', width: 381 },
    slots: {
      default: '<input aria-label="Рабочий email">',
      footer: 'Нет аккаунта?',
    },
  })

  expect(wrapper.get('form').attributes('novalidate')).toBeDefined()
  expect(wrapper.get('h1').text()).toBe('Войти в аккаунт')
  expect(wrapper.text()).toContain('SpecScribe')
  expect(wrapper.get('footer').text()).toBe('Нет аккаунта?')
  await wrapper.get('form').trigger('submit')
  expect(wrapper.emitted('submit')).toHaveLength(1)
})
