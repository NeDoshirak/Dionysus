import { runtimeConfig } from '@/shared/config/runtime'

let callbacks = {}

export function configureApi(nextCallbacks) {
  callbacks = { ...nextCallbacks }
}

function toRequestUrl(path) {
  return `${runtimeConfig.apiBaseUrl}${path}`
}

function buildRequestOptions({ method = 'GET', body, authenticated }) {
  const headers = { Accept: 'application/json' }
  const accessToken = authenticated ? callbacks.getAccessToken?.() : null

  if (accessToken) headers.Authorization = `Bearer ${accessToken}`
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'

  return {
    method,
    credentials: 'include',
    headers,
    body: body && !(body instanceof FormData) ? JSON.stringify(body) : body,
  }
}

export function getAuthenticatedFetchOptions() {
  const { credentials, headers } = buildRequestOptions({ authenticated: true })
  return { credentials, headers }
}

async function getPayload(response) {
  if (response.status === 204) return undefined

  const text = await response.text()
  if (!text) return undefined

  try {
    return JSON.parse(text)
  } catch {
    return undefined
  }
}

async function toApiError(response) {
  const payload = await getPayload(response)

  return {
    status: response.status,
    code: String(response.status),
    detail: payload?.detail || payload?.title || '',
  }
}

export async function apiRequest(path, { authenticated = false, retryOnUnauthorized = true, ...options } = {}) {
  const response = await fetch(toRequestUrl(path), buildRequestOptions({ ...options, authenticated }))

  if (authenticated && response.status === 401 && retryOnUnauthorized && await callbacks.refreshAccessToken?.()) {
    return apiRequest(path, { ...options, authenticated, retryOnUnauthorized: false })
  }

  if (!response.ok) {
    if (authenticated && response.status === 401) callbacks.onUnauthorized?.()
    throw await toApiError(response)
  }

  return getPayload(response)
}
