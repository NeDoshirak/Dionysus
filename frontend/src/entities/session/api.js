import { signUpLocal, verifyEmailLocal } from '@/shared/api/local-adapters'
import { setSession } from './model'

export async function signUp(credentials) {
  return signUpLocal(credentials)
}

export async function verifyEmail(credentials) {
  const session = await verifyEmailLocal(credentials)
  setSession(session)

  return session
}
