<script setup>
import { RouterLink } from 'vue-router'
import { formatProjectDate, projectStatuses } from '@/entities/project'
import { exportSpecificationMarkdown } from '@/entities/specification'

defineProps({
  projects: { type: Array, default: () => [] },
  loading: Boolean,
  error: { type: String, default: '' },
})

async function exportProject(project) {
  const { blob, fileName } = await exportSpecificationMarkdown(project.id)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')

  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="project-list" aria-live="polite">
    <p v-if="loading" class="project-list__state">Загрузка проектов…</p>
    <p v-else-if="error" class="project-list__state project-list__state--error" role="alert">{{ error }}</p>
    <div v-else-if="!projects.length" class="project-list__empty">
      <span class="project-list__file" aria-hidden="true">▣</span>
      <span class="project-list__details">
        <strong>Проектов пока нет</strong>
        <small>Создайте проект, чтобы начать</small>
      </span>
    </div>
    <div v-else class="project-list__rows">
      <div v-for="project in projects" :key="project.id" class="project-list__row">
        <RouterLink class="project-list__main" :to="{ name: 'specification', params: { id: project.id } }">
          <span class="project-list__file" aria-hidden="true">▣</span>
          <span class="project-list__details">
            <strong>{{ project.name }}</strong>
            <small>Транскрипция встречи</small>
          </span>
          <span class="project-list__status" :class="`project-list__status--${project.status}`">{{ projectStatuses[project.status] || project.status }}</span>
          <time class="project-list__date" :datetime="project.createdAt">{{ formatProjectDate(project.createdAt) }}</time>
        </RouterLink>
        <button class="project-list__export" type="button" aria-label="Экспортировать спецификацию" title="Экспортировать спецификацию" @click="exportProject(project)">
          <span aria-hidden="true">↓</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
.project-list
  &__rows
    display: grid

  &__empty
    display: grid
    justify-items: center
    gap: 12px
    padding: 64px 24px
    text-align: center

    .project-list__details
      justify-items: center

  &__row
    display: flex
    align-items: center
    gap: 16px
    min-height: 68px
    padding: 8px 16px 8px 0
    border-bottom: 1px solid var(--color-border-subtle)
    border-radius: 10px

    &:hover
      background: var(--color-accent-soft)

  &__main
    display: flex
    align-items: center
    flex: 1
    gap: 16px
    min-width: 0
    align-self: stretch
    padding: 6px 0 6px 16px
    color: var(--color-text)
    text-decoration: none

  &__file
    display: grid
    flex: 0 0 52px
    width: 52px
    height: 52px
    place-items: center
    border-radius: var(--radius-md)
    background: var(--color-accent-soft)
    color: var(--color-accent)

  &__details
    display: grid
    flex: 1
    gap: 3px
    min-width: 0

    strong
      overflow: hidden
      font-size: 15px
      font-weight: 500
      text-overflow: ellipsis
      white-space: nowrap

    small
      color: var(--color-subtle)
      font-size: 13px

  &__status
    padding: 2px 10px
    border-radius: 100px
    background: #ecfdf5
    color: #059669
    font-size: 12px

    &--processing
      background: #fff7ed
      color: #d97706

    &--failed
      background: #fef2f0
      color: var(--color-danger)

  &__date
    color: var(--color-muted)
    font-size: 13px
    white-space: nowrap

  &__export
    display: grid
    flex: 0 0 36px
    width: 36px
    height: 36px
    place-items: center
    border: 1px solid var(--color-border-subtle)
    border-radius: 50%
    background: var(--color-surface)
    color: var(--color-accent)
    cursor: pointer
    font-size: 18px
    line-height: 1

    &:hover
      border-color: var(--color-accent)
      background: var(--color-accent)
      color: #ffffff

  &__state
    padding: 24px 16px
    color: var(--color-muted)
    text-align: center

    &--error
      color: var(--color-danger)
</style>
