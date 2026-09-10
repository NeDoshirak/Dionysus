import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import FileDropzone from './FileDropzone.vue'

const maxBytes = 100 * 1024 * 1024

function setup(props = {}) {
  return mount(FileDropzone, { props: { accept: ['audio/*', 'video/*'], maxBytes, ...props } })
}

async function choose(wrapper, files) {
  const input = wrapper.get('input[type="file"]')
  Object.defineProperty(input.element, 'files', { configurable: true, value: files })
  await input.trigger('change')
}

describe('FileDropzone', () => {
  it.each(['audio/mpeg', 'video/mp4'])('emits an accepted %s file', async (type) => {
    const wrapper = setup()
    const file = new File(['meeting'], 'meeting.media', { type })
    await choose(wrapper, [file])
    expect(wrapper.emitted('select')).toEqual([[file]])
    expect(wrapper.emitted('reject')).toBeUndefined()
  })

  it('accepts a media file exactly at the size limit', async () => {
    const wrapper = setup()
    const file = new File(['meeting'], 'meeting.mp3', { type: 'audio/mpeg' })
    Object.defineProperty(file, 'size', { value: maxBytes })
    await choose(wrapper, [file])
    expect(wrapper.emitted('select')).toEqual([[file]])
  })

  it.each([
    ['text/plain', 10],
    ['audio/mpeg', maxBytes + 1],
    ['', 10],
  ])('rejects invalid media (%s, %i bytes) with a readable message', async (type, size) => {
    const wrapper = setup()
    const file = new File(['meeting'], 'meeting.txt', { type })
    Object.defineProperty(file, 'size', { value: size })
    await choose(wrapper, [file])
    expect(wrapper.emitted('select')).toBeUndefined()
    expect(wrapper.emitted('reject')?.[0]?.[0]).toEqual(expect.any(String))
    expect(wrapper.get('[role="alert"]').text().length).toBeGreaterThan(10)
  })

  it('validates dropped files and clears an earlier error after a valid selection', async () => {
    const wrapper = setup()
    await wrapper.trigger('drop', { dataTransfer: { files: [new File(['x'], 'notes.txt', { type: 'text/plain' })] } })
    expect(wrapper.emitted('reject')).toHaveLength(1)
    const file = new File(['meeting'], 'meeting.mp4', { type: 'video/mp4' })
    await wrapper.trigger('drop', { dataTransfer: { files: [file] } })
    expect(wrapper.emitted('select')).toEqual([[file]])
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
  })

  it('rejects multiple files rather than silently selecting one', async () => {
    const wrapper = setup()
    const file = new File(['meeting'], 'meeting.mp3', { type: 'audio/mpeg' })
    await wrapper.trigger('drop', { dataTransfer: { files: [file, file] } })
    expect(wrapper.emitted('select')).toBeUndefined()
    expect(wrapper.emitted('reject')).toHaveLength(1)
  })

  it('allows cancelling the picker without emitting an error', async () => {
    const wrapper = setup()
    await choose(wrapper, [])
    expect(wrapper.emitted('select')).toBeUndefined()
    expect(wrapper.emitted('reject')).toBeUndefined()
  })

  it('supports a specific MIME accept rule', async () => {
    const wrapper = setup({ accept: ['audio/mpeg'] })
    await choose(wrapper, [new File(['x'], 'meeting.mp4', { type: 'video/mp4' })])
    expect(wrapper.emitted('reject')).toHaveLength(1)
  })
})
