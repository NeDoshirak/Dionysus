<script setup>
import { computed, ref } from 'vue'
import {
  analysisStatusLabels,
  createSpecificationFunction,
  createSpecificationItem,
  deleteSpecificationFunction,
  deleteSpecificationItem,
  getTranscriptSegments,
  isSpecificationEditable,
  sortItems,
  updateSpecificationFunction,
  updateSpecificationItem,
} from '@/entities/specification'
import {
  SpecificationCardForm,
  SpecificationConfirmDialog,
  SpecificationFunctionForm,
} from '@/features/manage-specification'
import { BaseButton, StatusMessage } from '@/shared/ui'

import SpecificationFunctionSection from './SpecificationFunctionSection.vue'
import SpecificationPlayer from './SpecificationPlayer.vue'
import SpecificationTranscript from './SpecificationTranscript.vue'

const props = defineProps({
  projectId: { type: String, required: true },
  specification: { type: Object, default: null },
  project: { type: Object, default: null },
  mutationError: { type: String, default: '' },
})

const emit = defineEmits(['refresh', 'changed'])

const itemOrder = ref('server')
const transcriptQuery = ref('')
const activeSource = ref(null)
const clarificationIds = ref(new Set())
const playerRef = ref(null)

const functionForm = ref({ open: false, mode: 'create', functionItem: null, error: '' })
const cardForm = ref({ open: false, mode: 'create', functionItem: null, item: null, error: '' })
const confirmation = ref({ open: false, type: '', functionItem: null, item: null, error: '' })

const editable = computed(() => isSpecificationEditable(props.specification))
const statusLabel = computed(() => (
  analysisStatusLabels[props.specification?.status] || props.specification?.status || 'Нет анализа'
))
const businessContext = computed(() => props.specification?.businessContext || [])
const functions = computed(() => (
  [...(props.specification?.functions || [])].sort((left, right) => left.sortOrder - right.sortOrder)
))
const recording = computed(() => props.project?.recordings?.[0] || null)
const transcriptSegments = computed(() => getTranscriptSegments(props.project))
const nextFunctionSortOrder = computed(() => {
  if (!functions.value.length) return 0

  return Math.max(...functions.value.map((functionItem) => Number(functionItem.sortOrder) || 0)) + 1
})
const confirmationTitle = computed(() => (
  confirmation.value.type === 'function' ? 'Удалить функцию' : 'Удалить карточку'
))
const confirmationMessage = computed(() => {
  if (confirmation.value.type === 'function') {
    return 'Функция и все вложенные карточки будут удалены из спецификации.'
  }

  return 'Карточка будет удалена из спецификации.'
})

function sortedFunctionItems(functionItem) {
  return sortItems(functionItem.items || [], itemOrder.value)
}

function getErrorMessage(error) {
  return error?.detail || error?.message || 'Не удалось сохранить изменения'
}

function resetFunctionForm() {
  functionForm.value = { open: false, mode: 'create', functionItem: null, error: '' }
}

function resetCardForm() {
  cardForm.value = { open: false, mode: 'create', functionItem: null, item: null, error: '' }
}

function resetConfirmation() {
  confirmation.value = { open: false, type: '', functionItem: null, item: null, error: '' }
}

function openCreateFunction() {
  if (!editable.value) return
  functionForm.value = { open: true, mode: 'create', functionItem: null, error: '' }
}

function openEditFunction(functionItem) {
  if (!editable.value) return
  functionForm.value = { open: true, mode: 'edit', functionItem, error: '' }
}

function openCreateItem(functionItem) {
  if (!editable.value) return
  cardForm.value = { open: true, mode: 'create', functionItem, item: null, error: '' }
}

function openEditItem(functionItem, item) {
  if (!editable.value) return
  cardForm.value = { open: true, mode: 'edit', functionItem, item, error: '' }
}

function requestDeleteFunction(functionItem) {
  if (!editable.value) return
  confirmation.value = { open: true, type: 'function', functionItem, item: null, error: '' }
}

function requestDeleteItem(functionItem, item) {
  if (!editable.value) return
  confirmation.value = { open: true, type: 'item', functionItem, item, error: '' }
}

async function saveFunction(body) {
  functionForm.value.error = ''

  try {
    if (functionForm.value.mode === 'edit') {
      await updateSpecificationFunction(props.projectId, functionForm.value.functionItem.id, body)
    } else {
      await createSpecificationFunction(props.projectId, body)
    }
    resetFunctionForm()
    emit('changed')
  } catch (error) {
    functionForm.value.error = getErrorMessage(error)
  }
}

async function saveItem(body) {
  cardForm.value.error = ''
  const functionId = cardForm.value.functionItem?.id

  try {
    if (cardForm.value.mode === 'edit') {
      await updateSpecificationItem(props.projectId, functionId, cardForm.value.item.id, body)
    } else {
      await createSpecificationItem(props.projectId, functionId, body)
    }
    resetCardForm()
    emit('changed')
  } catch (error) {
    cardForm.value.error = getErrorMessage(error)
  }
}

async function confirmDelete() {
  confirmation.value.error = ''

  try {
    if (confirmation.value.type === 'function') {
      await deleteSpecificationFunction(props.projectId, confirmation.value.functionItem.id)
    } else {
      await deleteSpecificationItem(
        props.projectId,
        confirmation.value.functionItem.id,
        confirmation.value.item.id,
      )
    }
    resetConfirmation()
    emit('changed')
  } catch (error) {
    confirmation.value.error = getErrorMessage(error)
  }
}

function toggleClarification(itemId, clarified) {
  const nextIds = new Set(clarificationIds.value)
  if (clarified) {
    nextIds.add(itemId)
  } else {
    nextIds.delete(itemId)
  }
  clarificationIds.value = nextIds
}

function selectSource(source) {
  activeSource.value = source
  playerRef.value?.seek(Number(source.startSeconds) || 0)
}

function formatTime(seconds) {
  const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0))
  const minutes = Math.floor(safeSeconds / 60)
  const restSeconds = String(safeSeconds % 60).padStart(2, '0')

  return `${minutes}:${restSeconds}`
}
</script>

<template>
  <section class="specification-workspace" aria-label="Рабочая область ТЗ">
    <header class="specification-workspace__header">
      <div>
        <p class="specification-workspace__status">{{ statusLabel }}</p>
        <h1 class="specification-workspace__title">Техническое задание</h1>
        <p class="specification-workspace__description">
          {{ project?.name || 'Проект' }}
        </p>
      </div>

      <div class="specification-workspace__header-actions">
        <BaseButton
          v-if="editable"
          type="button"
          data-action="add-function"
          @click="openCreateFunction"
        >
          Добавить функцию
        </BaseButton>
        <BaseButton
          v-else
          type="button"
          variant="outline"
          @click="emit('refresh')"
        >
          Обновить
        </BaseButton>
      </div>
    </header>

    <StatusMessage v-if="mutationError" state="error">{{ mutationError }}</StatusMessage>

    <StatusMessage v-if="!specification" state="info">
      Спецификация для проекта пока недоступна.
    </StatusMessage>

    <template v-else-if="editable">
      <div class="specification-workspace__layout">
        <main class="specification-workspace__main">
          <section v-if="businessContext.length" class="specification-workspace__section">
            <div class="specification-workspace__section-header">
              <h2 class="specification-workspace__section-title">Контекст проекта</h2>
            </div>
            <div class="specification-workspace__cards">
              <article
                v-for="item in businessContext"
                :key="item.id"
                class="specification-workspace__context-card"
              >
                <span class="specification-workspace__context-kind">Контекст проекта</span>
                <h3 v-if="item.title" class="specification-workspace__context-title">{{ item.title }}</h3>
                <p class="specification-workspace__context-description">{{ item.description }}</p>
                <div v-if="item.sourceSegments?.length" class="specification-workspace__context-sources">
                  <button
                    v-for="(source, index) in item.sourceSegments"
                    :key="`${source.startSeconds}-${source.endSeconds}-${index}`"
                    class="specification-workspace__context-source"
                    type="button"
                    :data-source-start="source.startSeconds"
                    @click="selectSource(source)"
                  >
                    {{ formatTime(source.startSeconds) }}-{{ formatTime(source.endSeconds) }}
                    <span v-if="source.text">{{ source.text }}</span>
                  </button>
                </div>
              </article>
            </div>
          </section>

          <section class="specification-workspace__section">
            <div class="specification-workspace__section-header">
              <h2 class="specification-workspace__section-title">Функции</h2>
              <label class="specification-workspace__order">
                <span class="specification-workspace__order-label">Порядок карточек</span>
                <select v-model="itemOrder" name="item-order" class="specification-workspace__select">
                  <option value="server">Как в анализе</option>
                  <option value="type">По типу</option>
                </select>
              </label>
            </div>

            <div v-if="functions.length" class="specification-workspace__functions">
              <SpecificationFunctionSection
                v-for="functionItem in functions"
                :key="functionItem.id"
                :function-item="functionItem"
                :items="sortedFunctionItems(functionItem)"
                :editable="editable"
                :clarification-ids="clarificationIds"
                @add-item="openCreateItem"
                @edit-function="openEditFunction"
                @delete-function="requestDeleteFunction"
                @edit-item="openEditItem"
                @delete-item="requestDeleteItem"
                @source="selectSource"
                @toggle-clarification="toggleClarification"
              />
            </div>
            <StatusMessage v-else state="info">
              В спецификации пока нет функций.
            </StatusMessage>
          </section>
        </main>

        <aside class="specification-workspace__aside" aria-label="Запись и транскрипция">
          <SpecificationPlayer
            v-if="recording"
            ref="playerRef"
            :project-id="projectId"
            :recording="recording"
          />
          <StatusMessage v-else state="info">Запись встречи пока недоступна.</StatusMessage>

          <section class="specification-workspace__transcript-panel">
            <label class="specification-workspace__search">
              <span class="specification-workspace__search-label">Поиск по транскрипции</span>
              <input
                v-model="transcriptQuery"
                class="specification-workspace__search-input"
                name="transcript-query"
                type="search"
                autocomplete="off"
                placeholder="Введите фразу"
              >
            </label>
            <SpecificationTranscript
              :segments="transcriptSegments"
              :active-start-seconds="activeSource?.startSeconds ?? null"
              :query="transcriptQuery"
            />
          </section>
        </aside>
      </div>

      <SpecificationFunctionForm
        v-if="functionForm.open"
        :open="functionForm.open"
        :mode="functionForm.mode"
        :function-item="functionForm.functionItem"
        :sort-order="nextFunctionSortOrder"
        :error="functionForm.error"
        @save="saveFunction"
        @cancel="resetFunctionForm"
      />

      <SpecificationCardForm
        v-if="cardForm.open"
        :open="cardForm.open"
        :mode="cardForm.mode"
        :item="cardForm.item"
        :error="cardForm.error"
        @save="saveItem"
        @cancel="resetCardForm"
      />

      <SpecificationConfirmDialog
        v-if="confirmation.open"
        :open="confirmation.open"
        :title="confirmationTitle"
        :message="confirmationMessage"
        confirm-label="Удалить"
        @confirm="confirmDelete"
        @close="resetConfirmation"
      />
      <StatusMessage v-if="confirmation.error" state="error">{{ confirmation.error }}</StatusMessage>
    </template>

    <StatusMessage v-else state="info">
      Анализ сейчас в состоянии «{{ statusLabel }}». Обновите данные вручную, когда обработка завершится.
    </StatusMessage>
  </section>
</template>

<style scoped lang="sass">
.specification-workspace
  display: grid
  gap: 24px

  &__header
    display: flex
    align-items: flex-start
    justify-content: space-between
    gap: 20px

  &__status
    width: fit-content
    margin: 0 0 8px
    padding: 4px 10px
    border-radius: var(--radius-sm)
    background: var(--color-accent-soft)
    color: var(--color-accent-strong)
    font-size: 13px
    font-weight: 700
    line-height: 1.4

  &__title
    margin: 0
    color: var(--color-text)
    font-size: 36px
    line-height: 1.15

  &__description
    margin: 8px 0 0
    color: var(--color-muted)
    font-size: 16px
    line-height: 1.55

  &__header-actions
    flex-shrink: 0

  &__layout
    display: grid
    grid-template-columns: minmax(0, 1fr) minmax(320px, 390px)
    align-items: start
    gap: 24px

  &__main,
  &__section,
  &__functions,
  &__cards,
  &__aside,
  &__transcript-panel
    display: grid
    gap: 18px

  &__aside
    position: sticky
    top: 24px

  &__section-header
    display: flex
    align-items: flex-end
    justify-content: space-between
    gap: 16px

  &__section-title
    margin: 0
    color: var(--color-text)
    font-size: 24px
    line-height: 1.3

  &__order,
  &__search
    display: grid
    gap: 8px

  &__order
    min-width: 190px

  &__order-label,
  &__search-label
    color: var(--color-muted)
    font-size: 13px
    font-weight: 600
    line-height: 1.4

  &__select,
  &__search-input
    width: 100%
    min-height: 44px
    padding: 10px 12px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-surface)
    color: var(--color-text)
    font: inherit

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

  &__transcript-panel
    padding: 18px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-surface)

  &__context-card
    display: grid
    gap: 10px
    padding: 18px
    border: 1px solid var(--color-border)
    border-left: 4px solid var(--color-subtle)
    border-radius: var(--radius-sm)
    background: var(--color-surface)

  &__context-kind
    width: fit-content
    padding: 3px 8px
    border-radius: var(--radius-sm)
    background: var(--color-background)
    color: var(--color-muted)
    font-size: 12px
    font-weight: 600
    line-height: 1.4

  &__context-title
    margin: 0
    color: var(--color-text)
    font-size: 18px
    line-height: 1.35

  &__context-description
    margin: 0
    color: var(--color-muted)
    font-size: 14px
    line-height: 1.6

  &__context-sources
    display: grid
    gap: 8px

  &__context-source
    display: inline-grid
    gap: 4px
    justify-items: start
    width: fit-content
    max-width: 100%
    min-height: 36px
    padding: 7px 11px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-background)
    color: var(--color-accent-strong)
    font: inherit
    font-size: 13px
    font-weight: 700
    text-align: left
    cursor: pointer

    span
      max-width: 100%
      overflow: hidden
      color: var(--color-text-secondary)
      font-weight: 400
      text-overflow: ellipsis
      white-space: nowrap

    &:hover
      border-color: var(--color-accent-strong)
      background: var(--color-accent-soft)

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

@media (max-width: 960px)
  .specification-workspace
    &__layout
      grid-template-columns: 1fr

    &__aside
      position: static

@media (max-width: 640px)
  .specification-workspace
    &__header,
    &__section-header
      align-items: stretch
      flex-direction: column

    &__title
      font-size: 30px

    &__header-actions,
    &__order
      width: 100%
</style>
