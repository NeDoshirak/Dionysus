import { apiRequest } from '@/shared/api/client'
import { isDemoMode } from '@/shared/config/demo'
import { getDemoProject, getDemoProjectSummary } from '@/shared/demo/specification-workspace'

const demoProjectId = 'demo-project'

function toProjectSummary(project) {
  const { id, name, createdAt } = project
  const status = project.status || project.recordings?.at(-1)?.status || 'empty'

  return { id, name, createdAt, status }
}

export async function getProjects() {
  if (isDemoMode()) return [getDemoProjectSummary()]

  const projects = await apiRequest('/api/projects', { authenticated: true })
  return projects.map(toProjectSummary)
}

export async function findProjects(query) {
  const normalizedQuery = String(query || '').trim()
  if (normalizedQuery.length < 2) return []
  if (isDemoMode()) {
    const project = getDemoProjectSummary()
    return project.name.toLocaleLowerCase('ru').includes(normalizedQuery.toLocaleLowerCase('ru')) ? [project] : []
  }

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
  if (isDemoMode() && id === demoProjectId) return getDemoProject()

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
