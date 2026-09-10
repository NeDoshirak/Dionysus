import { expect, it } from 'vitest'

import { confirmPasswordResetLocal, requestPasswordResetLocal } from './local-adapters'

it.each(['person@example', 'person@example.', 'person @example.com', '@example.com'])('rejects malformed reset email %s', async (email) => {
  await expect(requestPasswordResetLocal({ email })).rejects.toMatchObject({ code: 'validation' })
})

it.each(['short1A', 'lowercase1', 'UPPERCASE'])('rejects reset passwords that miss a policy rule: %s', async (password) => {
  await expect(confirmPasswordResetLocal({ email: 'person@example.com', code: '123456', password })).rejects.toMatchObject({ code: 'validation' })
})

it('accepts a valid reset email and password', async () => {
  await expect(requestPasswordResetLocal({ email: ' person@example.com ' })).resolves.toEqual({ email: 'person@example.com' })
  await expect(confirmPasswordResetLocal({ email: 'person@example.com', code: '123456', password: 'Strong123' })).resolves.toEqual({ email: 'person@example.com' })
})
