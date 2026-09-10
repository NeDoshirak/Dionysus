import { beforeEach, expect, it, vi } from 'vitest'

import { apiRequest } from '@/shared/api/client'
import { clearSession, getSession } from './model'
import { confirmPasswordReset, requestPasswordReset, restoreSession, signIn, signOut, signUp, verifyEmail } from './api'

vi.mock('@/shared/api/client', () => ({ apiRequest: vi.fn() }))

beforeEach(() => {
  clearSession()
  vi.clearAllMocks()
})

it('returns the normalized email after body-less registration', async () => {
  apiRequest.mockResolvedValue(undefined)

  await expect(signUp({ email: ' Person@example.com ', password: 'Password1' })).resolves.toEqual({ email: 'person@example.com' })
  expect(apiRequest).toHaveBeenCalledWith('/api/auth/register', {
    method: 'POST',
    body: { email: 'person@example.com', password: 'Password1' },
  })
})

it('maps login forbidden responses to an unconfirmed-email error', async () => {
  apiRequest.mockRejectedValue({ status: 403 })

  await expect(signIn({ email: 'person@example.com', password: 'Password1' }))
    .rejects.toEqual({ code: 'unconfirmed-email', status: 403 })
})

it('stores the issued token and email after email verification', async () => {
  apiRequest.mockResolvedValue({ accessToken: 'access-token', expiresAt: '2026-09-11T12:00:00.000Z' })

  await verifyEmail({ email: 'person@example.com', code: '123456' })

  expect(getSession()).toEqual({ accessToken: 'access-token', expiresAt: '2026-09-11T12:00:00.000Z', email: 'person@example.com' })
})

it('restores an authenticated session from the refresh cookie and current user endpoint', async () => {
  apiRequest
    .mockResolvedValueOnce({ accessToken: 'refreshed-token', expiresAt: '2026-09-11T12:00:00.000Z' })
    .mockResolvedValueOnce({ email: 'person@example.com' })

  await expect(restoreSession()).resolves.toBe(true)

  expect(apiRequest).toHaveBeenNthCalledWith(1, '/api/auth/refresh', { method: 'POST' })
  expect(apiRequest).toHaveBeenNthCalledWith(2, '/api/auth/me', { authenticated: true })
  expect(getSession()).toEqual({ accessToken: 'refreshed-token', expiresAt: '2026-09-11T12:00:00.000Z', email: 'person@example.com' })
})

it('uses newPassword for password reset confirmation', async () => {
  apiRequest.mockResolvedValue(undefined)

  await confirmPasswordReset({ email: 'person@example.com', code: '123456', newPassword: 'Password1' })

  expect(apiRequest).toHaveBeenCalledWith('/api/auth/password-reset/confirm', {
    method: 'POST',
    body: { email: 'person@example.com', code: '123456', newPassword: 'Password1' },
  })
})

it('clears the session even when sign-out request fails', async () => {
  apiRequest
    .mockResolvedValueOnce({ accessToken: 'access-token', expiresAt: '2026-09-11T12:00:00.000Z' })
    .mockRejectedValueOnce({ status: 503 })
  await verifyEmail({ email: 'person@example.com', code: '123456' })

  await expect(signOut()).resolves.toBeUndefined()

  expect(getSession()).toBeNull()
})

it('keeps the entered email after password reset request', async () => {
  apiRequest.mockResolvedValue(undefined)

  await expect(requestPasswordReset({ email: ' Person@example.com ' })).resolves.toEqual({ email: 'person@example.com' })
})
