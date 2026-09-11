export const analysisStatusLabels = {
  queued: 'В очереди',
  runningStage0: 'Подготовка транскрипции',
  runningStage1: 'Выделение тем',
  runningStage2: 'Проверка фактов',
  runningStage3: 'Сборка ТЗ',
  completed: 'Готово',
  failed: 'Анализ не завершён',
}

export const analysisStatusValues = [
  'queued',
  'runningStage0',
  'runningStage1',
  'runningStage2',
  'runningStage3',
  'completed',
  'failed',
]

export const itemKindLabels = {
  businessContext: 'Контекст проекта',
  role: 'Роль',
  functionalRequirement: 'Требование',
  userScenario: 'Пользовательский сценарий',
  constraint: 'Ограничение',
  condition: 'Условие',
  agreement: 'Договорённость',
  keyQuestion: 'Открытый вопрос',
  projectContradiction: 'Противоречие',
}

export const itemKindValues = [
  'businessContext',
  'role',
  'functionalRequirement',
  'userScenario',
  'constraint',
  'condition',
  'agreement',
  'keyQuestion',
  'projectContradiction',
]

export const editableItemKinds = [
  'functionalRequirement',
  'role',
  'userScenario',
  'constraint',
  'condition',
  'agreement',
  'keyQuestion',
]

export const itemKindOrder = {
  functionalRequirement: 0,
  role: 1,
  userScenario: 2,
  constraint: 3,
  condition: 4,
  agreement: 5,
  keyQuestion: 6,
  businessContext: 7,
  projectContradiction: 8,
}

const enumValueToKey = (value, values) => {
  if (typeof value === 'number' && Number.isInteger(value)) return values[value] || value
  if (typeof value === 'string' && /^\d+$/.test(value)) return values[Number(value)] || value
  return value
}

const enumKeyToValue = (value, values) => {
  if (typeof value === 'number') return value
  if (typeof value === 'string' && /^\d+$/.test(value)) return Number(value)
  return values.indexOf(value) >= 0 ? values.indexOf(value) : value
}

export const normalizeAnalysisStatus = (status) => enumValueToKey(status, analysisStatusValues)
export const normalizeItemKind = (kind) => enumValueToKey(kind, itemKindValues)
export const toApiItemKind = (kind) => enumKeyToValue(kind, itemKindValues)

function normalizeSourceReferences(entity) {
  if (!entity || typeof entity !== 'object') return entity

  const sourceStatements = Array.isArray(entity.sourceStatements) ? entity.sourceStatements : []
  const sourceStatementIds = sourceStatements.length
    ? sourceStatements.map((statement) => statement?.id).filter(Boolean)
    : Array.isArray(entity.sourceStatementIds) ? [...entity.sourceStatementIds] : []

  return {
    ...entity,
    sourceStatementIds,
  }
}

function normalizeItem(item) {
  const normalized = normalizeSourceReferences(item)
  if (!normalized || typeof normalized !== 'object') return normalized

  return {
    ...normalized,
    kind: normalizeItemKind(normalized.kind),
  }
}

function normalizeFunction(functionItem) {
  const normalized = normalizeSourceReferences(functionItem)
  if (!normalized || typeof normalized !== 'object') return normalized

  return {
    ...normalized,
    items: Array.isArray(normalized.items) ? normalized.items.map(normalizeItem) : [],
  }
}

export const normalizeSpecification = (specification) => {
  if (!specification || typeof specification !== 'object') return specification

  return {
    ...specification,
    status: normalizeAnalysisStatus(specification.status),
    businessContext: Array.isArray(specification.businessContext)
      ? specification.businessContext.map(normalizeItem)
      : [],
    functions: Array.isArray(specification.functions)
      ? specification.functions.map(normalizeFunction)
      : [],
  }
}

export const toApiSpecificationItemBody = (body) => ({
  ...body,
  ...(Object.prototype.hasOwnProperty.call(body, 'kind') ? { kind: toApiItemKind(body.kind) } : {}),
  sourceStatementIds: Array.isArray(body.sourceStatementIds) ? [...body.sourceStatementIds] : [],
})

export const isSpecificationEditable = (specification) => normalizeAnalysisStatus(specification?.status) === 'completed'
export const getTranscriptSegments = (project) => project?.recordings?.[0]?.segments || []
export const sortItems = (items, order) => [...items].sort((left, right) => {
  if (order === 'type') {
    return itemKindOrder[left.kind] - itemKindOrder[right.kind] || left.sortOrder - right.sortOrder
  }

  return left.sortOrder - right.sortOrder
})
