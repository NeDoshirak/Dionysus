const localProjects = [
  {
    id: 'project-1',
    name: 'Customer discovery meeting',
    createdAt: '2026-09-10T08:30:00.000Z',
    status: 'completed',
    recordings: [
      {
        id: 'recording-1',
        fileName: 'discovery.mp3',
        segments: [
          {
            startSeconds: 0,
            endSeconds: 12,
            text: 'We discussed the project goals and priorities.',
          },
        ],
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
        fileName: media.name,
        segments: [],
      },
    ],
  }

  localProjects.unshift(project)

  return {
    ...project,
    recordings: project.recordings.map((recording) => ({
      ...recording,
      segments: [...recording.segments],
    })),
  }
}

export function getLocalProject(id) {
  const project = localProjects.find((item) => item.id === id)

  if (!project) return null

  return {
    ...project,
    recordings: project.recordings.map((recording) => ({
      ...recording,
      segments: recording.segments.map((segment) => ({ ...segment })),
    })),
  }
}

export async function signUpLocal({ email }) {
  return { email }
}

export async function verifyEmailLocal() {
  return {
    accessToken: 'local-access-token',
    expiresAt: '2026-09-11T12:00:00.000Z',
  }
}

export async function signInLocal({ email, password }) {
  if (!email?.trim() || !password) throw { code: 'validation' }
  if (email.trim().toLowerCase() === 'unconfirmed@dionysus.app') throw { code: 'unconfirmed-email' }
  if (email.trim().toLowerCase() !== 'demo@dionysus.app' || password !== 'Demo1234') throw { code: 'invalid-credentials' }

  return {
    accessToken: 'local-access-token',
    expiresAt: '2026-09-11T12:00:00.000Z',
  }
}

export async function requestPasswordResetLocal({ email }) {
  if (!email?.trim() || !email.includes('@')) throw { code: 'validation' }
  if (email.trim().toLowerCase() === 'server-error@dionysus.app') throw { code: 'server-error' }
  return { email: email.trim() }
}

export async function confirmPasswordResetLocal({ email, code, password }) {
  if (!email?.trim() || code !== '123456' || !password) throw { code: 'server-error' }
  return { email: email.trim() }
}
