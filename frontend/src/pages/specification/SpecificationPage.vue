<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { getProject } from '@/entities/project'
import {
  analysisStatusLabels,
  getSpecification,
  isSpecificationEditable,
  retrySpecification,
} from '@/entities/specification'
import { SpecificationConfirmDialog } from '@/features/manage-specification'
import { AppHeader } from '@/widgets/app-header'
import { SpecificationWorkspace } from '@/widgets/specification-workspace'
import { BaseButton, StatusMessage } from '@/shared/ui'

const route = useRoute()

const project = ref(null)
const specification = ref(null)
const loading = ref(true)
const error = ref(null)
const errorSource = ref('')
const retryOpen = ref(false)
const retryLoading = ref(false)
const retryError = ref('')

let loadRequestId = 0
let refreshTimer = null

const projectName = computed(() => (
  project.value?.name || String(route.query.name || `Проект ${route.params.id}`)
))

const statusLabel = computed(() => (
  analysisStatusLabels[specification.value?.status]
    || specification.value?.status
    || 'Анализ ТЗ'
))

const recording = computed(() => project.value?.recordings?.at(-1) || null)

const viewState = computed(() => {
  if (loading.value) return 'loading'
  if (errorSource.value === 'project') return 'error'
  if (recording.value?.status === 'failed') return 'transcription-failed'
  if (recording.value?.status === 'processing') return 'transcription-processing'
  if (error.value?.status === 404) return 'empty'
  if (error.value) return 'error'
  if (specification.value?.status === 'failed') return 'failed'
  if (!isSpecificationEditable(specification.value)) return 'processing'
  return 'completed'
})

const errorMessage = computed(() => {
  if (error.value?.detail) return error.value.detail
  if (error.value?.message) return error.value.message
  if (errorSource.value === 'project') return 'Не удалось загрузить проект.'
  return 'Не удалось загрузить анализ ТЗ.'
})

const failedAnalysisMessage = computed(() => (
  specification.value?.error || specification.value?.errorMessage || 'Не удалось завершить анализ ТЗ.'
))

const failedTranscriptionMessage = computed(() => (
  recording.value?.error || 'Не удалось расшифровать запись.'
))

function getProjectId() {
  return String(route.params.id)
}

async function loadWorkspace() {
  const requestId = ++loadRequestId
  const projectId = getProjectId()

  loading.value = true
  error.value = null
  errorSource.value = ''

  try {
    project.value = await getProject(projectId)
  } catch (reason) {
    if (requestId !== loadRequestId || projectId !== getProjectId()) return
    project.value = null
    specification.value = null
    error.value = reason
    errorSource.value = 'project'
    loading.value = false
    scheduleRefresh()
    return
  }

  if (requestId !== loadRequestId || projectId !== getProjectId()) return

  const currentRecording = project.value?.recordings?.at(-1)
  if (currentRecording?.status === 'processing' || currentRecording?.status === 'failed') {
    specification.value = null
    loading.value = false
    scheduleRefresh()
    return
  }

  try {
    specification.value = await getSpecification(projectId)
  } catch (reason) {
    if (requestId !== loadRequestId || projectId !== getProjectId()) return
    specification.value = null
    error.value = reason
    errorSource.value = 'specification'
  }

  loading.value = false
  scheduleRefresh()
}

function clearRefreshTimer() {
  if (refreshTimer === null) return
  clearTimeout(refreshTimer)
  refreshTimer = null
}

function scheduleRefresh() {
  clearRefreshTimer()
  if (recording.value?.status !== 'processing') return

  refreshTimer = setTimeout(() => {
    refreshTimer = null
    loadWorkspace()
  }, 3000)
}

function openRetry() {
  retryError.value = ''
  retryOpen.value = true
}

function closeRetry() {
  if (retryLoading.value) return
  retryOpen.value = false
  retryError.value = ''
}

async function confirmRetry() {
  if (retryLoading.value) return

  const projectId = getProjectId()
  retryLoading.value = true
  retryError.value = ''

  try {
    await retrySpecification(projectId)
    if (projectId !== getProjectId()) return
    retryOpen.value = false
    await loadWorkspace()
  } catch (reason) {
    retryError.value = reason?.detail || reason?.message || 'Не удалось повторить анализ ТЗ.'
  } finally {
    retryLoading.value = false
  }
}

function refreshWorkspace() {
  return loadWorkspace()
}

onMounted(loadWorkspace)
onBeforeUnmount(clearRefreshTimer)
watch(() => route.params.id, () => {
  clearRefreshTimer()
  loadWorkspace()
})
</script>

<template>
  <div class="specification-page">
    <AppHeader />
    <main class="specification-page__main">
      <div class="specification-page__heading">
        <div>
          <p class="specification-page__eyebrow">Проект</p>
          <h1 class="specification-page__title">{{ projectName }}</h1>
        </div>
        <RouterLink class="specification-page__back" :to="{ name: 'projects' }">← Вернуться к проектам</RouterLink>
      </div>

      <StatusMessage v-if="viewState === 'loading'" state="loading">
        Загружаем проект и техническое задание...
      </StatusMessage>

      <StatusMessage v-else-if="viewState === 'error'" state="error">
        {{ errorMessage }}
      </StatusMessage>

      <StatusMessage v-else-if="viewState === 'empty'" state="info">
        Анализ ТЗ пока недоступен.
      </StatusMessage>

      <StatusMessage v-else-if="viewState === 'transcription-failed'" state="error">
        {{ failedTranscriptionMessage }}
      </StatusMessage>

      <StatusMessage v-else-if="viewState === 'transcription-processing'" state="info">
        Расшифровываем запись. Страница обновляется автоматически.
      </StatusMessage>

      <section v-else-if="viewState === 'failed'" class="specification-page__state">
        <StatusMessage state="error">
          <strong>{{ statusLabel }}</strong>
          <span class="specification-page__state-description">{{ failedAnalysisMessage }}</span>
        </StatusMessage>
        <BaseButton type="button" data-action="retry-analysis" @click="openRetry">
          Повторить анализ
        </BaseButton>
      </section>

      <section v-else-if="viewState === 'processing'" class="specification-page__state">
        <StatusMessage state="info">
          Анализ ТЗ: {{ statusLabel }}. Обновите данные вручную, когда обработка завершится.
        </StatusMessage>
        <BaseButton type="button" variant="outline" data-action="refresh-analysis" @click="refreshWorkspace">
          Обновить
        </BaseButton>
      </section>

      <SpecificationWorkspace
        v-else
        :project-id="getProjectId()"
        :project="project"
        :specification="specification"
        @changed="refreshWorkspace"
        @refresh="refreshWorkspace"
      />

      <div v-if="retryOpen" class="specification-page__retry-confirm" data-action="confirm-retry" @click.self="confirmRetry">
        <SpecificationConfirmDialog
          :open="retryOpen"
          title="Повторить анализ ТЗ?"
          message="Текущий неудачный анализ будет запущен заново."
          confirm-label="Повторить"
          :error="retryError"
          :loading="retryLoading"
          @confirm="confirmRetry"
          @close="closeRetry"
        />
      </div>
    </main>
  </div>
</template>

<style scoped lang="sass">
.specification-page
  min-height: 100vh
  background: var(--color-background)

  &__main
    width: min(100%, 1120px)
    margin: 0 auto
    padding: 40px 32px 64px

  &__heading
    display: flex
    align-items: flex-end
    justify-content: space-between
    gap: 24px
    margin-bottom: 32px

  &__eyebrow
    margin: 0 0 8px
    color: var(--color-muted)
    font-size: 12px
    font-weight: 600
    letter-spacing: .8px
    text-transform: uppercase

  &__title
    margin: 0
    color: var(--color-text)
    font-size: 34px
    line-height: 1.2

  &__back
    flex-shrink: 0
    color: var(--color-accent-strong)
    font-size: 14px
    font-weight: 600
    text-decoration: none

    &:hover
      text-decoration: underline
      text-underline-offset: 4px

  &__state
    display: flex
    align-items: flex-start
    justify-content: space-between
    gap: 20px

  &__state-description
    display: block
    margin-top: 4px

  &__retry-confirm
    position: relative

@media (max-width: 720px)
  .specification-page
    &__main
      padding: 28px 20px 48px

    &__heading,
    &__state
      align-items: stretch
      flex-direction: column

    &__heading
      gap: 16px

    &__title
      font-size: 28px

    &__back
      order: -1
</style>
