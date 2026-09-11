import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SpecificationCardForm from './SpecificationCardForm.vue'

function mountForm(props = {}) {
  return mount(SpecificationCardForm, {
    props: { open: true, mode: 'create', ...props },
    global: {
      stubs: {
        BaseDialog: { template: '<section><slot /></section>' },
      },
    },
  })
}

describe('SpecificationCardForm', () => {
  it('emits a create body for a required functional requirement', async () => {
    const wrapper = mountForm()

    await wrapper.get('[name="kind"]').setValue('functionalRequirement')
    await wrapper.get('[name="title"]').setValue('SSO')
    await wrapper.get('textarea[name="description"]').setValue('Use corporate sign-in')
    await wrapper.get('[name="priority"]').setValue('required')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')[0]).toEqual([{
      kind: 'functionalRequirement',
      title: 'SSO',
      description: 'Use corporate sign-in',
      priority: 'required',
      sourceStatementIds: [],
    }])
  })

  it('uses empty source statements when create mode receives a residual item', async () => {
    const wrapper = mountForm({
      item: {
        id: 'item-previous',
        kind: 'functionalRequirement',
        title: 'Previous title',
        description: 'Previous description',
        priority: 'desirable',
        sourceStatementIds: ['statement-leak'],
      },
    })

    await wrapper.get('[name="kind"]').setValue('functionalRequirement')
    await wrapper.get('[name="title"]').setValue('SSO')
    await wrapper.get('textarea[name="description"]').setValue('Use corporate sign-in')
    await wrapper.get('[name="priority"]').setValue('required')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')[0]).toEqual([{
      kind: 'functionalRequirement',
      title: 'SSO',
      description: 'Use corporate sign-in',
      priority: 'required',
      sourceStatementIds: [],
    }])
  })

  it('preserves source statements and omits priority when editing a role', async () => {
    const wrapper = mountForm({
      mode: 'edit',
      item: {
        id: 'item-1',
        kind: 'role',
        title: 'Operator',
        description: 'Handles support requests',
        priority: 'future',
        sourceStatementIds: ['statement-1', 'statement-2'],
      },
    })

    await wrapper.get('[name="title"]').setValue('Support lead')
    await wrapper.get('textarea[name="description"]').setValue('Owns escalations')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.find('[name="kind"]').exists()).toBe(false)
    expect(wrapper.find('[name="priority"]').exists()).toBe(false)
    expect(wrapper.emitted('save')[0]).toEqual([{
      title: 'Support lead',
      description: 'Owns escalations',
      sourceStatementIds: ['statement-1', 'statement-2'],
    }])
  })

  it('uses reason values for key question priority', async () => {
    const wrapper = mountForm()

    await wrapper.get('[name="kind"]').setValue('keyQuestion')
    await wrapper.get('[name="title"]').setValue('Owner')
    await wrapper.get('textarea[name="description"]').setValue('Who approves access?')

    const options = wrapper.findAll('[name="priority"] option').map((option) => option.element.value)
    expect(options).toEqual(['contradiction', 'unresolved', 'missingInformation'])

    await wrapper.get('[name="priority"]').setValue('missingInformation')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')[0]).toEqual([{
      kind: 'keyQuestion',
      title: 'Owner',
      description: 'Who approves access?',
      priority: 'missingInformation',
      sourceStatementIds: [],
    }])
  })

  it('validates description before save and shows request errors without resetting values', async () => {
    const wrapper = mountForm({ error: 'Не удалось сохранить карточку' })

    const kindOptions = wrapper.findAll('[name="kind"] option').map((option) => option.element.value)
    expect(kindOptions).toEqual([
      'functionalRequirement',
      'role',
      'userScenario',
      'constraint',
      'condition',
      'agreement',
      'keyQuestion',
    ])

    await wrapper.get('[name="title"]').setValue('SSO')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('save')).toBeUndefined()
    expect(wrapper.text()).toContain('Добавьте описание')
    expect(wrapper.text()).toContain('Не удалось сохранить карточку')
    expect(wrapper.get('[name="title"]').element.value).toBe('SSO')
  })
})
