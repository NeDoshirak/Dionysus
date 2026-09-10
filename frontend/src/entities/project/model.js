export const projectStatuses = Object.freeze({
  completed: 'Готово',
  processing: 'Обработка',
  failed: 'Ошибка',
})

export function formatProjectDate(value) {
  return new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value))
}
