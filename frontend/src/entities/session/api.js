import { apiRequest } from '@/shared/api/client'
import { clearSession, getSession, setSession } from './model'

function normalizeEmail(email) {
  return String(email || '').trim().toLowerCase()
}

function setTokenSession(token, email = getSession()?.email || '') {
  const session = { ...token, email }
  setSession(session)
  return session
}

export async function signUp({ email, password }) {
  const normalizedEmail = normalizeEmail(email)
  await apiRequest('/api/auth/register', {
    method: 'POST',
    body: { email: normalizedEmail, password },
  })
  return { email: normalizedEmail }
}

export async function verifyEmail({ email, code }) {
  const normalizedEmail = normalizeEmail(email)
  const token = await apiRequest('/api/auth/verify-email', {
    method: 'POST',
    body: { email: normalizedEmail, code },
  })
  return setTokenSession(token, normalizedEmail)
}

export async function signIn({ email, password }) {
  const normalizedEmail = normalizeEmail(email)
  try {
    const token = await apiRequest('/api/auth/login', {
      method: 'POST',
      body: { email: normalizedEmail, password },
    })
    return setTokenSession(token, normalizedEmail)
  } catch (error) {
    if (error.status === 403) throw { code: 'unconfirmed-email', status: 403 }
    throw error
  }
}

export async function refreshSession() {
  const token = await apiRequest('/api/auth/refresh', { method: 'POST' })
  return setTokenSession(token)
}

export async function getCurrentUser() {
  return apiRequest('/api/auth/me', { authenticated: true })
}

export async function restoreSession() {
  if (getSession()) return true
  try {
    const token = await refreshSession()
    const user = await getCurrentUser()
    setTokenSession(token, user.email)
    return true
  } catch {
    clearSession()
    return false
  }
}

export async function signOut() {
  try {
    await apiRequest('/api/auth/logout', { method: 'POST' })
  } catch {
    // Local session cleanup must not block the user from leaving the account.
  } finally {
    clearSession()
  }
}

export async function requestPasswordReset({ email }) {
  const normalizedEmail = normalizeEmail(email)
  await apiRequest('/api/auth/password-reset/request', {
    method: 'POST',
    body: { email: normalizedEmail },
  })
  return { email: normalizedEmail }
}

export async function confirmPasswordReset({ email, code, newPassword }) {
  return apiRequest('/api/auth/password-reset/confirm', {
    method: 'POST',
    body: { email: normalizeEmail(email), code, newPassword },
  })
}
