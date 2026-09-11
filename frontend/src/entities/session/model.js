import { ref } from 'vue'

const sessionRef = ref(null)

export function setSession(nextSession) {
  sessionRef.value = nextSession ? { ...nextSession } : null
}

export function getSession() {
  return sessionRef.value ? { ...sessionRef.value } : null
}

export function clearSession() {
  sessionRef.value = null
}

// опционально, если где-то нужен реактивный доступ напрямую
export function useSession() {
  return sessionRef
}