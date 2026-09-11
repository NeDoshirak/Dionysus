import { beforeEach, expect, it, vi } from 'vitest'

import { apiRequest } from '@/shared/api/client'
import {
  createSpecificationItem,
  getSpecification,
  retrySpecification,
} from './api'

vi.mock('@/shared/api/client', () => ({ apiRequest: vi.fn() }))

beforeEach(() => {
  vi.clearAllMocks()
})

it('uses the completed specification endpoint with authentication', async () => {
  apiRequest.mockResolvedValue({ id: 'analysis-1', status: 'completed' })

  await getSpecification('project 1')

  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project%201/specification', { authenticated: true })
})

it('sends only contract field names when creating a card', async () => {
  apiRequest.mockResolvedValue({ id: 'item-1' })

  await createSpecificationItem('project-1', 'function-1', {
    kind: 'functionalRequirement',
    title: 'SSO',
    description: 'Use corporate sign-in',
    priority: 'required',
    sourceStatementIds: [],
  })

  expect(apiRequest).toHaveBeenCalledWith(
    '/api/projects/project-1/specification/functions/function-1/items',
    expect.objectContaining({
      method: 'POST',
      authenticated: true,
      body: {
        kind: 'functionalRequirement',
        title: 'SSO',
        description: 'Use corporate sign-in',
        priority: 'required',
        sourceStatementIds: [],
      },
    }),
  )
})

it('retries only through the documented retry path', async () => {
  apiRequest.mockResolvedValue({ analysisId: 'analysis-1', runId: 'run-2' })

  await retrySpecification('project-1')

  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project-1/specification/retry', { method: 'POST', authenticated: true })
})
