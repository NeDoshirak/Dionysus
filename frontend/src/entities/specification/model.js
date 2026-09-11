export const analysisStatusLabels = {
  queued: 'В очереди',
  runningStage0: 'Подготовка транскрипции',
  runningStage1: 'Выделение тем',
  runningStage2: 'Проверка фактов',
  runningStage3: 'Сборка ТЗ',
  completed: 'Готово',
  failed: 'Анализ не завершён',
}

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

const itemKindOrder = {
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

export const isSpecificationEditable = (specification) => specification?.status === 'completed'
export const getTranscriptSegments = (project) => project?.recordings?.[0]?.segments || []
export const sortItems = (items, order) => [...items].sort((left, right) => {
  if (order === 'type') {
    return itemKindOrder[left.kind] - itemKindOrder[right.kind] || left.sortOrder - right.sortOrder
  }

  return left.sortOrder - right.sortOrder
})
