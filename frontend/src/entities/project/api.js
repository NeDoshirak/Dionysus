import { createLocalProject, getLocalProjects, searchLocalProjects } from '@/shared/api/local-adapters'

export async function getProjects() {
  return getLocalProjects()
}

export async function findProjects(query) {
  const normalizedQuery = String(query || '').trim()
  if (normalizedQuery.length < 2) return []
  return searchLocalProjects(normalizedQuery)
}

export async function createProject({ name, media }) {
  if (!String(name || '').trim() || !(media instanceof File)) {
    throw { code: 'validation' }
  }

  if (!media.type.startsWith('audio/') && !media.type.startsWith('video/')) {
    throw { code: 'validation' }
  }

  if (media.size > 104857600) throw { code: 'validation' }

  return createLocalProject({ name: name.trim(), media })
}
