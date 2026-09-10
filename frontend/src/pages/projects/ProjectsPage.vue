<script setup>
import { onMounted, ref } from 'vue'
import { AppHeader } from '@/widgets/app-header'
import { ProjectList } from '@/widgets/project-list'
import { ProjectSearch } from '@/features/search-projects'
import { getProjects } from '@/entities/project'

const projects = ref([])
const baseProjects = ref([])
const loading = ref(true)
const error = ref('')
const showingSearchResults = ref(false)

async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    const loadedProjects = await getProjects()
    baseProjects.value = loadedProjects
    projects.value = loadedProjects
  } catch {
    error.value = 'Не удалось загрузить проекты'
  } finally {
    loading.value = false
  }
}

function updateResults(results) {
  showingSearchResults.value = results.length > 0
  projects.value = results.length ? results : baseProjects.value
}

onMounted(loadProjects)
</script>

<template>
  <div class="projects-page">
    <AppHeader />
    <main class="projects-page__main">
      <div class="projects-page__heading">
        <h1>Мои проекты</h1>
        <ProjectSearch @results="updateResults" />
      </div>
      <p class="projects-page__eyebrow">{{ showingSearchResults ? 'Результаты поиска' : 'Недавние проекты' }}</p>
      <ProjectList :projects="projects" :loading="loading" :error="error" />
    </main>
  </div>
</template>

<style scoped lang="sass">
.projects-page
  min-height: 100vh
  background: var(--color-background)

  &__main
    max-width: 960px
    margin: 0 auto
    padding: 36px 32px

  &__heading
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px

  h1
    margin: 0
    color: var(--color-text)
    font-size: 22px

  &__eyebrow
    margin: 32px 0 8px
    color: #9ca3af
    font-size: 12px
    font-weight: 600
    letter-spacing: .8px
    text-transform: uppercase

@media (max-width: 680px)
  .projects-page
    &__main
      padding: 24px 16px

    &__heading
      align-items: stretch
      flex-direction: column
</style>
