import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import { apiRequest } from '@/shared/api/client'
import {
  createSpecificationItem,
  getSpecification,
  retrySpecification,
  updateSpecificationItem,
} from './api'

vi.mock('@/shared/api/client', () => ({ apiRequest: vi.fn() }))

beforeEach(() => {
  vi.clearAllMocks()
})

afterEach(() => {
  vi.unstubAllEnvs()
})

it('normalizes numeric backend enums and preserves source evidence IDs', async () => {
  apiRequest.mockResolvedValue({
    id: 'analysis-1',
    status: 5,
    businessContext: [{ id: 'context-1', kind: 0, description: 'Context', sourceStatements: [{ id: 'statement-1' }] }],
    functions: [{
      id: 'function-1',
      sourceStatements: [{ id: 'statement-2' }],
      items: [{
        id: 'item-1',
        kind: 2,
        description: 'Requirement',
        sourceStatements: [{ id: 'statement-3' }],
      }],
    }],
  })

  await expect(getSpecification('project 1')).resolves.toMatchObject({
    status: 'completed',
    businessContext: [{ kind: 'businessContext', sourceStatementIds: ['statement-1'] }],
    functions: [{
      sourceStatementIds: ['statement-2'],
      items: [{ kind: 'functionalRequirement', sourceStatementIds: ['statement-3'] }],
    }],
  })

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
        kind: 2,
        title: 'SSO',
        description: 'Use corporate sign-in',
        priority: 'required',
        sourceStatementIds: [],
      },
    }),
  )
})

it('sends preserved source IDs and a numeric kind when editing a card', async () => {
  apiRequest.mockResolvedValue({ id: 'item-1' })

  await updateSpecificationItem('project-1', 'function-1', 'item-1', {
    title: 'SSO',
    description: 'Use corporate sign-in',
    sourceStatementIds: ['statement-1'],
  })

  expect(apiRequest).toHaveBeenCalledWith(
    '/api/projects/project-1/specification/functions/function-1/items/item-1',
    expect.objectContaining({
      method: 'PATCH',
      authenticated: true,
      body: {
        title: 'SSO',
        description: 'Use corporate sign-in',
        sourceStatementIds: ['statement-1'],
      },
    }),
  )
})

it('retries only through the documented retry path', async () => {
  apiRequest.mockResolvedValue({ analysisId: 'analysis-1', runId: 'run-2' })

  await retrySpecification('project-1')

  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project-1/specification/retry', { method: 'POST', authenticated: true })
})

it('serves a completed local demo specification without an API request when demo mode is enabled', async () => {
  vi.stubEnv('VITE_DEMO_MODE', 'true')

  await expect(getSpecification('demo-project')).resolves.toEqual(expect.objectContaining({
    id: 'demo-specification',
    status: 'completed',
    businessContext: expect.any(Array),
    functions: expect.any(Array),
  }))

  expect(apiRequest).not.toHaveBeenCalled()
})
