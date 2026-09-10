let session = null

export function setSession(nextSession) {
  session = { ...nextSession }
}

export function getSession() {
  return session ? { ...session } : null
}
