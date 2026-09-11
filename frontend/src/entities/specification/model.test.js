import { expect, it } from 'vitest'

import { editableItemKinds, itemKindOrder } from './index'
import {
  getTranscriptSegments,
  isSpecificationEditable,
  normalizeAnalysisStatus,
  normalizeItemKind,
  normalizeSpecification,
  sortItems,
} from './model'

it('exposes editable kinds and deterministic type order from the public API', () => {
  expect(editableItemKinds).toEqual([
    'functionalRequirement',
    'role',
    'userScenario',
    'constraint',
    'condition',
    'agreement',
    'keyQuestion',
  ])
  expect(itemKindOrder.functionalRequirement).toBe(0)
  expect(itemKindOrder.projectContradiction).toBe(8)
})

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
  expect(isSpecificationEditable({ status: 5 })).toBe(true)
  expect(isSpecificationEditable({ status: 'runningStage2' })).toBe(false)
  expect(getTranscriptSegments({ recordings: [{ segments: [{ startSeconds: 4, endSeconds: 6, text: 'Hello' }] }] })).toHaveLength(1)
})

it('maps numeric contract enums to stable UI keys', () => {
  expect(normalizeAnalysisStatus(0)).toBe('queued')
  expect(normalizeAnalysisStatus(6)).toBe('failed')
  expect(normalizeItemKind(1)).toBe('role')
  expect(normalizeItemKind(8)).toBe('projectContradiction')
})

it('derives editable source IDs from persisted source statement objects', () => {
  const normalized = normalizeSpecification({
    status: 5,
    businessContext: [],
    functions: [{
      items: [{ kind: 3, sourceStatements: [{ id: 'statement-1', text: 'Source' }] }],
    }],
  })

  expect(normalized.functions[0].items[0]).toMatchObject({
    kind: 'userScenario',
    sourceStatementIds: ['statement-1'],
  })
})

it('uses legacy source IDs only when the contract has no source statement objects', () => {
  const normalized = normalizeSpecification({
    status: 5,
    businessContext: [],
    functions: [{
      items: [{ kind: 1, sourceStatements: [], sourceStatementIds: ['legacy-statement'] }],
    }],
  })

  expect(normalized.functions[0].items[0].sourceStatementIds).toEqual(['legacy-statement'])
})
