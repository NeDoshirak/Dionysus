import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SpecificationFunctionForm from './SpecificationFunctionForm.vue'

function mountForm(props = {}) {
  return mount(SpecificationFunctionForm, {
    props: { open: true, mode: 'create', sortOrder: 3, ...props },
    global: {
      stubs: {
        BaseDialog: { template: '<section><slot /></section>' },
      },
    },
  })
}

describe('SpecificationFunctionForm', () => {
  it('emits a create body with parent-provided sort order and no sources', async () => {
    const wrapper = mountForm({ sortOrder: 7 })

    await wrapper.get('[name="title"]').setValue('Authentication')
    await wrapper.get('textarea[name="description"]').setValue('Access control and sign-in')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')[0]).toEqual([{
      title: 'Authentication',
      description: 'Access control and sign-in',
      sortOrder: 7,
      sourceStatementIds: [],
    }])
  })

  it('uses empty source statements when create mode receives a residual function', async () => {
    const wrapper = mountForm({
      sortOrder: 7,
      functionItem: {
        id: 'function-previous',
        title: 'Previous function',
        description: 'Previous description',
        sortOrder: 2,
        sourceStatementIds: ['statement-leak'],
      },
    })

    await wrapper.get('[name="title"]').setValue('Authentication')
    await wrapper.get('textarea[name="description"]').setValue('Access control and sign-in')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')[0]).toEqual([{
      title: 'Authentication',
      description: 'Access control and sign-in',
      sortOrder: 7,
      sourceStatementIds: [],
    }])
  })

  it('preserves source statements when editing and keeps values with request errors', async () => {
    const wrapper = mountForm({
      mode: 'edit',
      error: 'Не удалось сохранить функцию',
      functionItem: {
        id: 'function-1',
        title: 'Reporting',
        description: 'Build exports',
        sortOrder: 4,
        sourceStatementIds: ['statement-9'],
      },
    })

    await wrapper.get('[name="title"]').setValue('Analytics')
    await wrapper.get('textarea[name="description"]').setValue('Show operating metrics')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.text()).toContain('Не удалось сохранить функцию')
    expect(wrapper.get('[name="title"]').element.value).toBe('Analytics')
    expect(wrapper.emitted('save')[0]).toEqual([{
      title: 'Analytics',
      description: 'Show operating metrics',
      sortOrder: 4,
      sourceStatementIds: ['statement-9'],
    }])
  })

  it('validates description before save', async () => {
    const wrapper = mountForm()

    await wrapper.get('[name="title"]').setValue('Authentication')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')).toBeUndefined()
    expect(wrapper.text()).toContain('Добавьте описание')
  })
})
