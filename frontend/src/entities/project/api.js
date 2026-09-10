import { getLocalProjects, searchLocalProjects } from '@/shared/api/local-adapters'

export async function getProjects() {
  return getLocalProjects()
}

export async function findProjects(query) {
  const normalizedQuery = String(query || '').trim()
  if (normalizedQuery.length < 2) return []
  return searchLocalProjects(normalizedQuery)
}
