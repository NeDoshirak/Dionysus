const transcriptSegments = [
  { id: 'demo-segment-1', startSeconds: 0, endSeconds: 18, text: 'Команда обсуждает запуск личного кабинета для корпоративных клиентов.' },
  { id: 'demo-segment-2', startSeconds: 18, endSeconds: 39, text: 'Сотрудник должен входить через корпоративный SSO и видеть только свои организации.' },
  { id: 'demo-segment-3', startSeconds: 39, endSeconds: 63, text: 'На первом этапе нужны заявки на подключение и уведомления о смене статуса.' },
  { id: 'demo-segment-4', startSeconds: 63, endSeconds: 88, text: 'Срок пилота — конец квартала, а вопросы по интеграции с CRM требуют уточнения.' },
]

const source = (id, startSeconds, endSeconds, text) => ({
  id,
  startSeconds,
  endSeconds,
  text,
})

const demoProject = {
  id: 'demo-project',
  name: 'Демо: запуск личного кабинета',
  createdAt: '2026-09-11T06:00:00.000Z',
  status: 'completed',
  recordings: [{
    id: 'demo-recording',
    status: 'completed',
    streamUrl: null,
    durationSeconds: 88,
    waveformPeaks: [[.14, .35, .58, .28, .73, .41, .9, .22, .62, .34, .77, .46, .18, .67, .3, .82]],
    segments: transcriptSegments,
  }],
}

const demoSpecification = {
  id: 'demo-specification',
  status: 'completed',
  createdAt: '2026-09-11T06:05:00.000Z',
  completedAt: '2026-09-11T06:06:00.000Z',
  businessContext: [{
    id: 'demo-context',
    kind: 'businessContext',
    title: 'Цель запуска',
    description: 'Сократить время подключения корпоративных клиентов и сделать статус заявок прозрачным.',
    sortOrder: 0,
    isManual: false,
    sourceStatements: [{ id: 'demo-statement-context' }],
    sourceSegments: [source('demo-source-context', 0, 18, transcriptSegments[0].text)],
  }],
  functions: [{
    id: 'demo-function-access',
    title: 'Доступ и подключение',
    description: 'Функции, с которых начинается работа клиента в личном кабинете.',
    sortOrder: 0,
    sourceStatements: [{ id: 'demo-statement-function' }],
    items: [
      {
        id: 'demo-item-sso',
        kind: 'functionalRequirement',
        title: 'Вход через корпоративный SSO',
        description: 'Система должна поддерживать вход через SSO и ограничивать доступ организациями пользователя.',
        priority: 'Высокий',
        sortOrder: 0,
        isManual: false,
        sourceStatements: [{ id: 'demo-statement-sso' }],
        sourceSegments: [source('demo-source-sso', 18, 39, transcriptSegments[1].text)],
      },
      {
        id: 'demo-item-request',
        kind: 'userScenario',
        title: 'Подача заявки на подключение',
        description: 'Клиент создаёт заявку, прикладывает данные организации и отслеживает её статус.',
        priority: 'Высокий',
        sortOrder: 1,
        isManual: false,
        sourceStatements: [{ id: 'demo-statement-request' }],
        sourceSegments: [source('demo-source-request', 39, 63, transcriptSegments[2].text)],
      },
      {
        id: 'demo-item-crm',
        kind: 'keyQuestion',
        title: 'Интеграция с CRM',
        description: 'Нужно согласовать, какие поля заявки и статусы синхронизируются с CRM.',
        reason: 'Нет решения',
        sortOrder: 2,
        isManual: false,
        sourceStatements: [{ id: 'demo-statement-crm' }],
        sourceSegments: [source('demo-source-crm', 63, 88, transcriptSegments[3].text)],
      },
    ],
  }],
}

function clone(value) {
  return JSON.parse(JSON.stringify(value))
}

export function getDemoProject() {
  return clone(demoProject)
}

export function getDemoProjectSummary() {
  const { id, name, createdAt, status } = demoProject
  return { id, name, createdAt, status }
}

export function getDemoSpecification() {
  return clone(demoSpecification)
}
