import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import { apiRequest, configureApi, getAuthenticatedFetchOptions } from './client'

function jsonResponse(payload, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  configureApi({})
})

afterEach(() => {
  vi.unstubAllGlobals()
})

it('returns refresh-cookie and bearer settings for protected media', () => {
  configureApi({ getAccessToken: () => 'wave-token' })

  expect(getAuthenticatedFetchOptions()).toEqual({
    credentials: 'include',
    headers: { Accept: 'application/json', Authorization: 'Bearer wave-token' },
  })
})

it('adds a bearer token and credentials to a protected JSON request', async () => {
  configureApi({ getAccessToken: () => 'access-token' })
  const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ projects: [] }))
  vi.stubGlobal('fetch', fetchMock)

  await expect(apiRequest('/api/projects', { authenticated: true })).resolves.toEqual({ projects: [] })

  expect(fetchMock).toHaveBeenCalledWith('/api/projects', expect.objectContaining({
    credentials: 'include',
    headers: expect.objectContaining({
      Accept: 'application/json',
      Authorization: 'Bearer access-token',
    }),
  }))
})

it('refreshes once and retries a protected request after an unauthorized response', async () => {
  const refreshAccessToken = vi.fn().mockResolvedValue(true)
  configureApi({
    getAccessToken: () => 'renewed-token',
    refreshAccessToken,
  })
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(jsonResponse([]))
  vi.stubGlobal('fetch', fetchMock)

  await expect(apiRequest('/api/projects', { authenticated: true })).resolves.toEqual([])

  expect(refreshAccessToken).toHaveBeenCalledOnce()
  expect(fetchMock).toHaveBeenCalledTimes(2)
  expect(fetchMock.mock.calls[1][1].headers.Authorization).toBe('Bearer renewed-token')
})

it('exposes HTTP error information without raw response payloads', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({ detail: 'Only audio is supported' }, 422)))

  await expect(apiRequest('/api/projects', { method: 'POST' })).rejects.toEqual({
    status: 422,
    code: '422',
    detail: 'Only audio is supported',
  })
})
