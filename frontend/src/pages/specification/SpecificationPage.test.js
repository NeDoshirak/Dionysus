import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { nextTick } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getProject, searchProjectTranscription } from '@/entities/project'
import { getSpecification, retrySpecification } from '@/entities/specification'

import SpecificationPage from './SpecificationPage.vue'

vi.mock('@/entities/project', () => ({
  getProject: vi.fn(),
  searchProjectTranscription: vi.fn(),
}))

vi.mock('@/entities/specification', () => ({
  getSpecification: vi.fn(),
  retrySpecification: vi.fn(),
  analysisStatusLabels: {
    queued: 'В очереди',
    runningStage2: 'Проверка фактов',
    completed: 'Готово',
    failed: 'Анализ не завершён',
  },
  isSpecificationEditable: (value) => value?.status === 'completed',
}))

vi.mock('@/widgets/specification-workspace', () => ({
  SpecificationWorkspace: {
    name: 'SpecificationWorkspace',
    props: {
      projectId: { type: String, required: true },
      project: { type: Object, required: true },
      specification: { type: Object, required: true },
      mutationError: { type: String, default: '' },
    },
    emits: ['refresh', 'changed'],
    template: `
      <section class="specification-workspace">
        <p>{{ project.name }}</p>
        <p>{{ specification.status }}</p>
        <p v-if="mutationError">{{ mutationError }}</p>
        <button type="button" data-action="workspace-refresh" @click="$emit('refresh')">Обновить workspace</button>
        <button type="button" data-action="workspace-changed" @click="$emit('changed')">Сохранено workspace</button>
      </section>
    `,
  },
}))

const projectWithTranscript = {
  id: 'project-1',
  name: 'Discovery',
  recordings: [
    {
      id: 'recording-1',
      segments: [
        { startSeconds: 14, endSeconds: 18, text: 'Use corporate sign-in' },
      ],
    },
  ],
}

const completedSpecification = {
  id: 'analysis-1',
  status: 'completed',
  businessContext: [],
  functions: [],
  sourceStatements: [],
  sourceSegments: [],
}

function createTestRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/projects/:id/specification', name: 'specification', component: SpecificationPage },
      { path: '/projects', name: 'projects', component: { template: '<div />' } },
      { path: '/profile', name: 'profile', component: { template: '<div />' } },
    ],
  })
}

async function mountPage(projectId = 'project-1') {
  const router = createTestRouter()
  await router.push({ name: 'specification', params: { id: projectId } })
  await router.isReady()

  const wrapper = mount(SpecificationPage, {
    global: {
      plugins: [router],
    },
  })

  return { router, wrapper }
}

describe('SpecificationPage', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the completed workspace after loading the project and specification', async () => {
    getProject.mockResolvedValue(projectWithTranscript)
    getSpecification.mockResolvedValue(completedSpecification)

    const { router, wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.find('.specification-workspace').exists()).toBe(true))
    expect(wrapper.text()).toContain('Discovery')
    expect(getProject).toHaveBeenCalledWith('project-1')
    expect(getSpecification).toHaveBeenCalledWith('project-1')
    expect(searchProjectTranscription).not.toHaveBeenCalled()

    await wrapper.get('.specification-page__back').trigger('click')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('projects'))
  })

  it('shows a manual refresh while analysis is running and does not start an interval', async () => {
    const intervalSpy = vi.spyOn(window, 'setInterval')
    getProject.mockResolvedValue(projectWithTranscript)
    getSpecification.mockResolvedValue({ id: 'analysis-1', status: 'runningStage2' })

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Проверка фактов'))
    expect(wrapper.text()).toContain('Обновить')
    expect(intervalSpy).not.toHaveBeenCalled()

    await wrapper.get('[data-action="refresh-analysis"]').trigger('click')
    await vi.waitFor(() => expect(getSpecification).toHaveBeenCalledTimes(2))
    expect(getProject).toHaveBeenCalledTimes(2)
    expect(intervalSpy).not.toHaveBeenCalled()
    intervalSpy.mockRestore()
  })

  it('polls project details after three seconds while transcription is processing', async () => {
    const timerSpy = vi.spyOn(global, 'setTimeout').mockImplementation(() => 1)
    getProject.mockResolvedValue({ ...projectWithTranscript, recordings: [{ id: 'recording-1', status: 'processing', segments: [] }] })
    getSpecification.mockRejectedValue({ status: 404 })

    await mountPage('project-1')

    await vi.waitFor(() => expect(timerSpy).toHaveBeenCalledWith(expect.any(Function), 3000))
    const [reload] = timerSpy.mock.calls.find(([, delay]) => delay === 3000)
    await reload()
    await vi.waitFor(() => expect(getProject).toHaveBeenCalledTimes(2))
    expect(getSpecification).toHaveBeenCalledTimes(2)
    timerSpy.mockRestore()
  })

  it('stops transcription polling after a terminal recording status', async () => {
    const timerSpy = vi.spyOn(global, 'setTimeout').mockImplementation(() => 1)
    getProject
      .mockResolvedValueOnce({ ...projectWithTranscript, recordings: [{ id: 'recording-1', status: 'processing', segments: [] }] })
      .mockResolvedValueOnce({ ...projectWithTranscript, recordings: [{ id: 'recording-1', status: 'completed', segments: [] }] })
    getSpecification.mockRejectedValue({ status: 404 })

    await mountPage('project-1')

    await vi.waitFor(() => expect(timerSpy).toHaveBeenCalledWith(expect.any(Function), 3000))
    const [reload] = timerSpy.mock.calls.find(([, delay]) => delay === 3000)
    await reload()
    await vi.waitFor(() => expect(getProject).toHaveBeenCalledTimes(2))
    expect(timerSpy.mock.calls.filter(([, delay]) => delay === 3000)).toHaveLength(1)
    timerSpy.mockRestore()
  })

  it('shows the safe transcription error after a failed recording', async () => {
    getProject.mockResolvedValue({
      ...projectWithTranscript,
      recordings: [{ id: 'recording-1', status: 'failed', error: 'Transcription failed.', segments: [] }],
    })
    getSpecification.mockRejectedValue({ status: 404 })

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Transcription failed.'))
    expect(wrapper.text()).not.toContain('Анализ ТЗ пока недоступен')
  })

  it('clears the scheduled transcription refresh when the page unmounts', async () => {
    const timerSpy = vi.spyOn(global, 'setTimeout').mockImplementation(() => 42)
    const clearTimerSpy = vi.spyOn(global, 'clearTimeout')
    getProject.mockResolvedValue({ ...projectWithTranscript, recordings: [{ id: 'recording-1', status: 'processing', segments: [] }] })
    getSpecification.mockRejectedValue({ status: 404 })

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(timerSpy).toHaveBeenCalledWith(expect.any(Function), 3000))
    wrapper.unmount()
    expect(clearTimerSpy).toHaveBeenCalledWith(42)
  })

  it('ignores a stale load after navigating to another project', async () => {
    const firstProject = createDeferred()
    const firstSpecification = createDeferred()
    getProject.mockReturnValueOnce(firstProject.promise).mockResolvedValue({ ...projectWithTranscript, id: 'project-2', name: 'Second' })
    getSpecification.mockReturnValueOnce(firstSpecification.promise).mockResolvedValue({ ...completedSpecification, id: 'analysis-2' })

    const { router, wrapper } = await mountPage('project-1')
    await router.push({ name: 'specification', params: { id: 'project-2' } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Second'))
    firstProject.resolve({ ...projectWithTranscript, id: 'project-1', name: 'Stale first project' })
    firstSpecification.resolve({ ...completedSpecification, id: 'stale-analysis' })
    await nextTick()

    expect(wrapper.text()).toContain('Second')
    expect(wrapper.text()).not.toContain('Stale first project')
  })

  it('shows a no-analysis state for a specification 404 after the project loads', async () => {
    getProject.mockResolvedValue(projectWithTranscript)
    getSpecification.mockRejectedValue({ status: 404 })

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Анализ ТЗ пока недоступен'))
    expect(wrapper.find('.specification-workspace').exists()).toBe(false)
  })

  it('shows the project loading error when project loading fails alongside a specification 404', async () => {
    getProject.mockRejectedValue({ message: 'Проект не найден' })
    getSpecification.mockRejectedValue({ status: 404 })

    const { wrapper } = await mountPage('missing-project')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Проект не найден'))
    expect(wrapper.text()).not.toContain('Анализ ТЗ пока недоступен')
    expect(wrapper.find('.specification-workspace').exists()).toBe(false)
  })

  it('refreshes the loaded data once after a workspace mutation event', async () => {
    getProject.mockResolvedValue(projectWithTranscript)
    getSpecification.mockResolvedValue(completedSpecification)

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.find('.specification-workspace').exists()).toBe(true))
    await wrapper.get('[data-action="workspace-changed"]').trigger('click')
    await vi.waitFor(() => expect(getSpecification).toHaveBeenCalledTimes(2))

    expect(getProject).toHaveBeenCalledTimes(2)
    expect(searchProjectTranscription).not.toHaveBeenCalled()
  })

  it('confirms retry for failed analysis and reloads the workspace once after retry succeeds', async () => {
    getProject.mockResolvedValue(projectWithTranscript)
    getSpecification
      .mockResolvedValueOnce({ id: 'analysis-1', status: 'failed', error: 'LLM unavailable' })
      .mockResolvedValueOnce(completedSpecification)
    retrySpecification.mockResolvedValue({ analysisId: 'analysis-1', runId: 'run-2' })

    const { wrapper } = await mountPage('project-1')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Анализ не завершён'))
    expect(wrapper.text()).toContain('LLM unavailable')
    await wrapper.get('[data-action="retry-analysis"]').trigger('click')
    await nextTick()
    await wrapper.get('[data-action="confirm-retry"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('.specification-workspace').exists()).toBe(true))
    expect(retrySpecification).toHaveBeenCalledTimes(1)
    expect(retrySpecification).toHaveBeenCalledWith('project-1')
    expect(getSpecification).toHaveBeenCalledTimes(2)
    expect(getProject).toHaveBeenCalledTimes(2)
  })
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
