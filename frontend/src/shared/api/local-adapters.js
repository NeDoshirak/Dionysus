const localProjects = [
  {
    id: 'project-1',
    name: 'Customer discovery meeting',
    createdAt: '2026-09-10T08:30:00.000Z',
    status: 'completed',
    recordings: [
      {
        id: 'recording-1',
        name: 'discovery.mp3',
      },
    ],
    transcriptSegments: [
      {
        startSeconds: 0,
        endSeconds: 12,
        text: 'We discussed the project goals and priorities.',
      },
    ],
  },
]

const localSession = {
  email: 'demo@dionysus.app',
}

function toProjectSummary(project) {
  const { id, name, createdAt, status } = project

  return { id, name, createdAt, status }
}

export function getLocalSession() {
  return { ...localSession }
}

export function getLocalProjects() {
  return localProjects.map(toProjectSummary)
}

export function searchLocalProjects(query) {
  const normalizedQuery = query.trim().toLowerCase()

  if (normalizedQuery.length < 2) return []

  return getLocalProjects().filter((project) => project.name.toLowerCase().includes(normalizedQuery))
}

export function createLocalProject({ name, media }) {
  const project = {
    id: crypto.randomUUID(),
    name,
    createdAt: new Date().toISOString(),
    status: 'completed',
    recordings: [
      {
        id: crypto.randomUUID(),
        name: media.name,
      },
    ],
    transcriptSegments: [],
  }

  localProjects.unshift(project)

  return { ...project, recordings: [...project.recordings], transcriptSegments: [] }
}

export function getLocalProject(id) {
  const project = localProjects.find((item) => item.id === id)

  if (!project) return null

  return {
    ...project,
    recordings: project.recordings.map((recording) => ({ ...recording })),
    transcriptSegments: project.transcriptSegments.map((segment) => ({ ...segment })),
  }
}
