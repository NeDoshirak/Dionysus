import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import { apiRequest } from '@/shared/api/client'
import { createProject, findProjects, getProject, getProjects, searchProjectTranscription } from './api'

vi.mock('@/shared/api/client', () => ({ apiRequest: vi.fn() }))

function validMedia() {
  return new File(['audio'], 'meeting.mp3', { type: 'audio/mpeg' })
}

beforeEach(() => {
  vi.clearAllMocks()
})

afterEach(() => {
  vi.unstubAllEnvs()
})

it('loads authenticated project summaries', async () => {
  const projects = [{ id: '1', name: 'Discovery', createdAt: '2026-09-11T00:00:00Z', status: 'completed' }]
  apiRequest.mockResolvedValue(projects)

  await expect(getProjects()).resolves.toEqual(projects)
  expect(apiRequest).toHaveBeenCalledWith('/api/projects', { authenticated: true })
})

it('submits exactly name and media as multipart project data', async () => {
  apiRequest.mockResolvedValue({
    id: '1',
    name: 'Discovery',
    createdAt: '2026-09-11T00:00:00Z',
    recordings: [{ status: 'completed' }],
  })

  await createProject({ name: ' Discovery ', media: validMedia() })

  const [path, options] = apiRequest.mock.calls[0]
  expect(path).toBe('/api/projects')
  expect(options).toMatchObject({ method: 'POST', authenticated: true })
  expect([...options.body.keys()]).toEqual(['name', 'media'])
  expect(options.body.get('name')).toBe('Discovery')
  expect(options.body.get('media')).toBeInstanceOf(File)
})

it('converts created project details into a project summary', async () => {
  apiRequest.mockResolvedValue({
    id: '1',
    name: 'Discovery',
    createdAt: '2026-09-11T00:00:00Z',
    recordings: [{ status: 'completed' }],
  })

  await expect(createProject({ name: 'Discovery', media: validMedia() })).resolves.toEqual({
    id: '1',
    name: 'Discovery',
    createdAt: '2026-09-11T00:00:00Z',
    status: 'completed',
  })
})

it('does not send search requests for a one-character query', async () => {
  await expect(findProjects('a')).resolves.toEqual([])
  expect(apiRequest).not.toHaveBeenCalled()
})

it('encodes project and transcription search queries', async () => {
  apiRequest.mockResolvedValue([])

  await findProjects('new project')
  await searchProjectTranscription('project-id', 'important item')

  expect(apiRequest).toHaveBeenNthCalledWith(1, '/api/projects/search?query=new%20project', { authenticated: true })
  expect(apiRequest).toHaveBeenNthCalledWith(2, '/api/projects/project-id/transcription-search?query=important%20item', { authenticated: true })
})

it('gets project details through the authenticated endpoint', async () => {
  apiRequest.mockResolvedValue({ id: 'project-id' })

  await expect(getProject('project-id')).resolves.toEqual({ id: 'project-id' })
  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project-id', { authenticated: true })
})

it('serves the local demo project without API requests when demo mode is enabled', async () => {
  vi.stubEnv('VITE_DEMO_MODE', 'true')

  await expect(getProjects()).resolves.toEqual([
    expect.objectContaining({ id: 'demo-project', name: 'Демо: запуск личного кабинета', status: 'completed' }),
  ])
  await expect(getProject('demo-project')).resolves.toEqual(expect.objectContaining({
    id: 'demo-project',
    recordings: [expect.objectContaining({ id: 'demo-recording', segments: expect.any(Array) })],
  }))

  expect(apiRequest).not.toHaveBeenCalled()
})
