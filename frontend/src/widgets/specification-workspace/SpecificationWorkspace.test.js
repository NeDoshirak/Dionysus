import { mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { ref } from 'vue'

import SpecificationWorkspace from './SpecificationWorkspace.vue'

const api = vi.hoisted(() => ({
  createSpecificationFunction: vi.fn(),
  updateSpecificationFunction: vi.fn(),
  deleteSpecificationFunction: vi.fn(),
  createSpecificationItem: vi.fn(),
  updateSpecificationItem: vi.fn(),
  deleteSpecificationItem: vi.fn(),
  retrySpecification: vi.fn(),
}))

const projectApi = vi.hoisted(() => ({
  searchProjectTranscription: vi.fn(),
}))

const playerState = vi.hoisted(() => ({
  setTime: vi.fn(),
  play: vi.fn(),
  playPause: vi.fn(),
}))

vi.mock('@/entities/specification', async (importOriginal) => ({
  ...await importOriginal(),
  ...api,
}))

vi.mock('@/entities/project', () => projectApi)

vi.mock('@meersagor/wavesurfer-vue', () => ({
  useWaveSurfer: () => ({
    waveSurfer: ref({
      setTime: playerState.setTime,
      play: playerState.play,
      playPause: playerState.playPause,
    }),
    isReady: ref(true),
    isPlaying: ref(false),
    currentTime: ref(0),
    totalDuration: ref(120),
  }),
}))

vi.mock('@/shared/api/client', () => ({
  getAuthenticatedFetchOptions: () => ({ credentials: 'include', headers: {} }),
}))

const completedSpecification = {
  id: 'specification-1',
  status: 'completed',
  businessContext: [
    {
      id: 'context-1',
      kind: 'businessContext',
      title: 'Access context',
      description: 'The customer wants a predictable onboarding flow.',
      sortOrder: 0,
      sourceSegments: [{ startSeconds: 4, endSeconds: 8, text: 'onboarding flow' }],
    },
  ],
  functions: [
    {
      id: 'function-1',
      title: 'Authentication',
      description: 'Control access to the product.',
      sortOrder: 0,
      sourceStatementIds: ['statement-1'],
      items: [
        {
          id: 'role-1',
          kind: 'role',
          title: 'Security lead',
          description: 'Owns sign-in policies.',
          sortOrder: 0,
          sourceStatementIds: ['statement-2'],
          sourceSegments: [{ startSeconds: 38, endSeconds: 42, text: 'security lead' }],
        },
        {
          id: 'requirement-1',
          kind: 'functionalRequirement',
          title: 'SSO',
          description: 'Use corporate sign-in.',
          priority: 'required',
          sortOrder: 1,
          sourceStatementIds: ['statement-3'],
          sourceSegments: [{ startSeconds: 14, endSeconds: 18, text: 'corporate sign-in' }],
        },
      ],
    },
  ],
}

const projectWithTranscript = {
  id: 'project-1',
  name: 'Discovery',
  recordings: [
    {
      id: 'recording-1',
      segments: [
        { startSeconds: 4, endSeconds: 8, text: 'We need a predictable onboarding flow.' },
        { startSeconds: 14, endSeconds: 18, text: 'Use corporate sign-in for all users.' },
        { startSeconds: 38, endSeconds: 42, text: 'The security lead owns the policies.' },
      ],
    },
  ],
}

function mountWorkspace(props = {}) {
  return mount(SpecificationWorkspace, {
    props: {
      projectId: 'project-1',
      specification: completedSpecification,
      project: projectWithTranscript,
      mutationError: '',
      ...props,
    },
    global: {
      stubs: {
        BaseDialog: { template: '<section><slot /></section>' },
      },
    },
  })
}

beforeEach(() => {
  vi.clearAllMocks()
})

function createDeferred() {
  let resolve
  let reject
  const promise = new Promise((settle, fail) => {
    resolve = settle
    reject = fail
  })

  return { promise, resolve, reject }
}

it('sorts displayed cards by type without changing the input items', async () => {
  const wrapper = mountWorkspace()

  await wrapper.get('[name="item-order"]').setValue('type')

  expect(wrapper.findAll('.specification-card').map((card) => card.attributes('data-kind'))).toEqual([
    'functionalRequirement',
    'role',
  ])
  expect(completedSpecification.functions[0].items.map((item) => item.id)).toEqual(['role-1', 'requirement-1'])
})

it('seeks the waveform and selects transcript text when a source is clicked', async () => {
  const wrapper = mountWorkspace()

  await wrapper.get('[data-source-start="14"]').trigger('click')

  expect(playerState.setTime).toHaveBeenCalledWith(14)
  expect(playerState.play).not.toHaveBeenCalled()
  expect(playerState.playPause).not.toHaveBeenCalled()
  expect(wrapper.get('.specification-transcript__segment--active').text()).toContain('corporate sign-in')
})

it('filters the transcript locally without calling a search API', async () => {
  const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{}'))
  const wrapper = mountWorkspace()

  await wrapper.get('[name="transcript-query"]').setValue('security')

  expect(wrapper.findAll('.specification-transcript__segment')).toHaveLength(1)
  expect(wrapper.text()).toContain('security lead')
  expect(projectApi.searchProjectTranscription).not.toHaveBeenCalled()
  expect(fetchSpy).not.toHaveBeenCalled()
  fetchSpy.mockRestore()
})

it('keeps selected source evidence visible when a transcript filter is active', async () => {
  const wrapper = mountWorkspace()

  await wrapper.get('[name="transcript-query"]').setValue('security')
  await wrapper.get('[data-source-start="14"]').trigger('click')

  expect(wrapper.get('[name="transcript-query"]').element.value).toBe('')
  expect(wrapper.get('.specification-transcript__segment--active').text()).toContain('corporate sign-in')
  expect(playerState.setTime).toHaveBeenCalledWith(14)
  expect(playerState.play).not.toHaveBeenCalled()
  expect(playerState.playPause).not.toHaveBeenCalled()
})

it('emits changed only after a successful card mutation', async () => {
  api.createSpecificationItem.mockRejectedValueOnce({ detail: 'Validation failed' })
  const wrapper = mountWorkspace()

  await wrapper.get('[data-action="add-item-function-1"]').trigger('click')
  await wrapper.get('[name="kind"]').setValue('functionalRequirement')
  await wrapper.get('[name="title"]').setValue('SSO fallback')
  await wrapper.get('textarea[name="description"]').setValue('Allow backup sign-in.')
  await wrapper.get('form').trigger('submit')

  await vi.waitFor(() => expect(wrapper.text()).toContain('Validation failed'))
  expect(wrapper.emitted('changed')).toBeUndefined()

  api.createSpecificationItem.mockResolvedValueOnce({ id: 'requirement-2' })
  await wrapper.get('form').trigger('submit')

  await vi.waitFor(() => expect(wrapper.emitted('changed')).toHaveLength(1))
  expect(api.createSpecificationItem).toHaveBeenLastCalledWith('project-1', 'function-1', {
    kind: 'functionalRequirement',
    title: 'SSO fallback',
    description: 'Allow backup sign-in.',
    priority: 'required',
    sourceStatementIds: [],
  })
})

it('prevents duplicate card submissions while a request is pending', async () => {
  const pendingCreate = createDeferred()
  api.createSpecificationItem.mockReturnValueOnce(pendingCreate.promise)
  const wrapper = mountWorkspace()

  await wrapper.get('[data-action="add-item-function-1"]').trigger('click')
  await wrapper.get('[name="kind"]').setValue('functionalRequirement')
  await wrapper.get('[name="title"]').setValue('SSO fallback')
  await wrapper.get('textarea[name="description"]').setValue('Allow backup sign-in.')
  await wrapper.get('form').trigger('submit')
  await wrapper.get('form').trigger('submit')

  expect(api.createSpecificationItem).toHaveBeenCalledTimes(1)

  pendingCreate.resolve({ id: 'requirement-2' })
  await vi.waitFor(() => expect(wrapper.emitted('changed')).toHaveLength(1))
})

it('does not let a completed stale card request close a subsequently opened form', async () => {
  const pendingCreate = createDeferred()
  api.createSpecificationItem.mockReturnValueOnce(pendingCreate.promise)
  const wrapper = mountWorkspace()

  await wrapper.get('[data-action="add-item-function-1"]').trigger('click')
  await wrapper.get('[name="title"]').setValue('First draft')
  await wrapper.get('textarea[name="description"]').setValue('First description.')
  await wrapper.get('form').trigger('submit')
  await wrapper.find('.specification-card-form button[type="button"]').trigger('click')
  await wrapper.get('[data-action="add-item-function-1"]').trigger('click')
  await wrapper.get('[name="title"]').setValue('Second draft')

  pendingCreate.resolve({ id: 'requirement-2' })

  await vi.waitFor(() => expect(wrapper.emitted('changed')).toHaveLength(1))
  expect(wrapper.get('form').exists()).toBe(true)
  expect(wrapper.get('[name="title"]').element.value).toBe('Second draft')
})

it('keeps delete errors inside the confirmation dialog until retry or close', async () => {
  api.deleteSpecificationItem.mockRejectedValueOnce({ detail: 'Delete failed' })
  const wrapper = mountWorkspace()

  await wrapper.get('[data-action="delete-item-requirement-1"]').trigger('click')
  await wrapper.get('.specification-confirm-dialog button:last-child').trigger('click')

  await vi.waitFor(() => {
    expect(wrapper.get('.specification-confirm-dialog').text()).toContain('Delete failed')
  })
  expect(wrapper.emitted('changed')).toBeUndefined()

  api.deleteSpecificationItem.mockResolvedValueOnce()
  await wrapper.get('.specification-confirm-dialog button:last-child').trigger('click')

  await vi.waitFor(() => expect(wrapper.find('.specification-confirm-dialog').exists()).toBe(false))
  expect(wrapper.emitted('changed')).toHaveLength(1)
})
