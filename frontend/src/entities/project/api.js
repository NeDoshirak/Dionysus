import { apiRequest } from '@/shared/api/client'

function toProjectSummary(project) {
  const { id, name, createdAt } = project
  const status = project.status || project.recordings?.at(-1)?.status || 'empty'

  return { id, name, createdAt, status }
}

export async function getProjects() {
  const projects = await apiRequest('/api/projects', { authenticated: true })
  return projects.map(toProjectSummary)
}

export async function findProjects(query) {
  const normalizedQuery = String(query || '').trim()
  if (normalizedQuery.length < 2) return []
  const projects = await apiRequest(`/api/projects/search?query=${encodeURIComponent(normalizedQuery)}`, { authenticated: true })
  return projects.map(toProjectSummary)
}

export async function createProject({ name, media }) {
  const normalizedName = String(name || '').trim()
  if (!normalizedName || normalizedName.length > 200 || !(media instanceof File)) {
    throw { code: 'validation' }
  }

  if (!media.type.startsWith('audio/') && !media.type.startsWith('video/')) {
    throw { code: 'validation' }
  }

  if (media.size > 104857600) throw { code: 'validation' }

  const body = new FormData()
  body.append('name', normalizedName)
  body.append('media', media)

  const project = await apiRequest('/api/projects', {
    method: 'POST',
    body,
    authenticated: true,
  })

  return toProjectSummary(project)
}

export async function getProject(id) {
  return apiRequest(`/api/projects/${encodeURIComponent(id)}`, { authenticated: true })
}

export async function searchProjectTranscription(id, query) {
  const normalizedQuery = String(query || '').trim()
  if (normalizedQuery.length < 2) return []

  return apiRequest(
    `/api/projects/${encodeURIComponent(id)}/transcription-search?query=${encodeURIComponent(normalizedQuery)}`,
    { authenticated: true },
  )
}
