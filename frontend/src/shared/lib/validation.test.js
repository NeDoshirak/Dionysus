import { expect, it } from 'vitest'

import router from '@/app/router'
import {
  createLocalProject,
  getLocalProject,
  getLocalProjects,
  getLocalSession,
  searchLocalProjects,
} from '@/shared/api/local-adapters'
import { validatePassword } from './validation'

it('requires eight characters, a digit, and an uppercase letter', () => {
  expect(validatePassword('password').isValid).toBe(false)
  expect(validatePassword('Password1').isValid).toBe(true)
})

it('identifies every missing password policy rule', () => {
  expect(validatePassword('pass').errors).toEqual(['min-length', 'uppercase', 'digit'])
})

it('provides all planned named routes', () => {
  expect(router.getRoutes().map((route) => route.name)).toEqual(expect.arrayContaining([
    'landing',
    'sign-up',
    'sign-in',
    'verify-email',
    'reset-password',
    'projects',
    'specification',
    'profile',
  ]))
})

it('serves contract-shaped local session and project fixtures', () => {
  expect(getLocalSession()).toMatchObject({
    email: expect.any(String),
  })
  expect(getLocalProjects()[0]).toEqual(expect.objectContaining({
    id: expect.any(String),
    name: expect.any(String),
    createdAt: expect.any(String),
    status: expect.any(String),
  }))
})

it('searches, creates, and retrieves local projects without network access', () => {
  const media = new File(['meeting'], 'meeting.mp3', { type: 'audio/mpeg' })
  const project = createLocalProject({ name: 'Planning session', media })

  expect(searchLocalProjects('planning')).toEqual(expect.arrayContaining([
    expect.objectContaining({ id: project.id, name: 'Planning session' }),
  ]))
  const projectDetail = getLocalProject(project.id)

  expect(projectDetail).toMatchObject({
    id: project.id,
    name: 'Planning session',
    recordings: expect.arrayContaining([
      expect.objectContaining({
        fileName: 'meeting.mp3',
        segments: expect.any(Array),
      }),
    ]),
  })
  expect(projectDetail).not.toHaveProperty('transcriptSegments')
})
