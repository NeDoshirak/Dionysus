import { mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import ClarificationToggle from './ClarificationToggle.vue'

it('does not emit any request-shaped event for the local clarification marker', async () => {
  const wrapper = mount(ClarificationToggle, { props: { modelValue: false } })
  const fetchSpy = vi.spyOn(globalThis, 'fetch')

  await wrapper.get('input').setValue(true)

  expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
  expect(wrapper.emitted('save')).toBeUndefined()
  expect(wrapper.text()).toContain('Требует уточнения')
  expect(wrapper.text()).toContain('Локально: не сохраняется после обновления страницы.')
  expect(fetchSpy).not.toHaveBeenCalled()
  fetchSpy.mockRestore()
})
