import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import CreateProjectDialog from './CreateProjectDialog.vue'
import { FileDropzone } from '@/shared/ui'

const validFile = () => new File(['audio'], 'meeting.mp3', { type: 'audio/mpeg' })

function mountDialog(createProject = vi.fn().mockResolvedValue({ id: 'new-project', name: 'Discovery', createdAt: '2026-09-11T00:00:00.000Z', status: 'completed' })) {
  return mount(CreateProjectDialog, {
    props: { open: true, createProject },
    global: {
      stubs: {
        BaseDialog: { template: '<div><slot /></div>' },
      },
    },
  })
}

describe('CreateProjectDialog', () => {
  it('requires a project name and media before submitting', async () => {
    const createProject = vi.fn()
    const wrapper = mountDialog(createProject)

    await wrapper.get('form').trigger('submit')

    expect(wrapper.text()).toContain('Введите название проекта')
    expect(createProject).not.toHaveBeenCalled()
  })

  it('displays the selected file and submits name and media', async () => {
    const createProject = vi.fn().mockResolvedValue({ id: 'new-project', name: 'Discovery' })
    const wrapper = mountDialog(createProject)

    await wrapper.get('input[name="name"]').setValue('Discovery')
    wrapper.findComponent(FileDropzone).vm.$emit('select', validFile())
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('meeting.mp3')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createProject).toHaveBeenCalledWith({ name: 'Discovery', media: expect.any(File) })
    expect(wrapper.emitted('created')).toHaveLength(1)
  })

  it.each([
    ['422', { code: 'conversion-failed' }, 'Не удалось преобразовать запись'],
    ['502', { code: 'transcription-failed' }, 'Не удалось расшифровать запись'],
  ])('shows a server error for %s responses', async (_label, failure, message) => {
    const createProject = vi.fn().mockRejectedValue(failure)
    const wrapper = mountDialog(createProject)

    await wrapper.get('input[name="name"]').setValue('Discovery')
    wrapper.findComponent(FileDropzone).vm.$emit('select', validFile())
    await wrapper.vm.$nextTick()
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain(message)
    expect(wrapper.text()).toContain('Попробуйте ещё раз')
  })
})
