<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { AppHeader } from '@/widgets/app-header'
import { ProjectList } from '@/widgets/project-list'
import { ProjectSearch } from '@/features/search-projects'
import { CreateProjectDialog } from '@/features/create-project'
import { getProjects } from '@/entities/project'
import { BaseButton } from '@/shared/ui'
import plusIcon from '@/shared/assets/icons/plus.svg'

const router = useRouter()
const projects = ref([])
const baseProjects = ref([])
const loading = ref(true)
const error = ref('')
const showingSearchResults = ref(false)
const createDialogOpen = ref(false)

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

function handleCreated(project) {
  const summary = {
    id: project.id,
    name: project.name,
    createdAt: project.createdAt,
    status: project.status,
  }
  baseProjects.value = [summary, ...baseProjects.value.filter((item) => item.id !== summary.id)]
  projects.value = baseProjects.value
  showingSearchResults.value = false
  createDialogOpen.value = false
  router.push({ name: 'specification', params: { id: summary.id } })
}

onMounted(loadProjects)
</script>

<template>
  <div class="projects-page">
    <AppHeader />
    <main class="projects-page__main">
      <div class="projects-page__heading">
        <h1>Мои проекты</h1>
        <div class="projects-page__actions">
          <ProjectSearch @results="updateResults" />
          <BaseButton class="projects-page__create-button" aria-label="Новый проект" @click="createDialogOpen = true">
            <img :src="plusIcon" alt="" aria-hidden="true">
            <span class="visually-hidden">Новый проект</span>
          </BaseButton>
        </div>
      </div>
      <p class="projects-page__eyebrow">{{ showingSearchResults ? 'Результаты поиска' : 'Недавние проекты' }}</p>
      <ProjectList :projects="projects" :loading="loading" :error="error" />
    </main>
    <CreateProjectDialog :open="createDialogOpen" @created="handleCreated" @close="createDialogOpen = false" />
  </div>
</template>

<style scoped lang="sass">
.projects-page
  min-height: 100vh
  background: var(--color-background)

  &__main
    width: min(calc(100% - 48px), 845px)
    margin: 0 auto
    padding: 36px 0

  &__heading
    display: flex
    align-items: center
    justify-content: space-between
    gap: 12px

  &__actions
    display: flex
    align-items: center
    gap: 12px

  &__create-button
    width: 36px
    min-width: 36px
    min-height: 36px
    height: 36px
    padding: 0
    border-radius: 50%
    box-shadow: 0 2px 4px rgba(241, 54, 29, .3)

    img
      width: 18px
      height: 18px

  h1
    margin: 0
    color: var(--color-text)
    font-size: 22px

  &__eyebrow
    margin: 32px 0 8px
    color: var(--color-subtle)
    font-size: 12px
    font-weight: 600
    letter-spacing: .8px
    text-transform: uppercase

@media (max-width: 680px)
  .projects-page
    &__main
      width: calc(100% - 32px)
      padding: 24px 0

    &__heading
      align-items: stretch
      flex-direction: column

    &__actions
      align-items: stretch
      flex-direction: column

      :deep(.project-search)
        width: 100%
</style>
