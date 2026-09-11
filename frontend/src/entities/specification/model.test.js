import { expect, it } from 'vitest'

import { getTranscriptSegments, isSpecificationEditable, sortItems } from './model'

it('keeps server order or groups cards alphabetically by displayed kind', () => {
  const items = [
    { id: '2', kind: 'role', sortOrder: 1 },
    { id: '1', kind: 'functionalRequirement', sortOrder: 0 },
  ]

  expect(sortItems(items, 'server').map((item) => item.id)).toEqual(['1', '2'])
  expect(sortItems(items, 'type').map((item) => item.id)).toEqual(['1', '2'])
})

it('treats only completed analyses as editable and reads the sole recording transcript', () => {
  expect(isSpecificationEditable({ status: 'completed' })).toBe(true)
  expect(isSpecificationEditable({ status: 'runningStage2' })).toBe(false)
  expect(getTranscriptSegments({ recordings: [{ segments: [{ startSeconds: 4, endSeconds: 6, text: 'Hello' }] }] })).toHaveLength(1)
})
