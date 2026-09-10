<script setup>
import { RouterLink } from 'vue-router'
import { formatProjectDate, projectStatuses } from '@/entities/project'

defineProps({
  projects: { type: Array, default: () => [] },
  loading: Boolean,
  error: { type: String, default: '' },
})
</script>

<template>
  <div class="project-list" aria-live="polite">
    <p v-if="loading" class="project-list__state">Загрузка проектов…</p>
    <p v-else-if="error" class="project-list__state project-list__state--error" role="alert">{{ error }}</p>
    <p v-else-if="!projects.length" class="project-list__state">Проектов пока нет</p>
    <div v-else class="project-list__rows">
      <RouterLink v-for="project in projects" :key="project.id" class="project-list__row" :to="{ name: 'specification', params: { id: project.id } }">
        <span class="project-list__file" aria-hidden="true">▣</span>
        <span class="project-list__details">
          <strong>{{ project.name }}</strong>
          <small>Транскрипция встречи</small>
        </span>
        <span class="project-list__status" :class="`project-list__status--${project.status}`">{{ projectStatuses[project.status] || project.status }}</span>
        <time class="project-list__date" :datetime="project.createdAt">{{ formatProjectDate(project.createdAt) }}</time>
      </RouterLink>
    </div>
  </div>
</template>

<style scoped lang="sass">
.project-list
  &__rows
    display: grid

  &__row
    display: flex
    align-items: center
    gap: 16px
    min-height: 68px
    padding: 14px 16px
    border-bottom: 1px solid #f3f4f6
    border-radius: 10px
    color: var(--color-text)
    text-decoration: none

    &:hover
      background: var(--color-accent-soft)

  &__file
    display: grid
    width: 38px
    height: 38px
    place-items: center
    border-radius: 9px
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
      color: #9ca3af
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

  &__state
    padding: 24px 16px
    color: var(--color-muted)
    text-align: center

    &--error
      color: var(--color-danger)
</style>
