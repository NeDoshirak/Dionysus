<script setup>
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { AppHeader } from '@/widgets/app-header'

const route = useRoute()
const projectName = computed(() => String(route.query.name || `Проект ${route.params.id}`))
</script>

<template>
  <div class="specification-page">
    <AppHeader />
    <main class="specification-page__main">
      <p class="specification-page__eyebrow">Проект</p>
      <h1>{{ projectName }}</h1>
      <section class="specification-page__placeholder">
        <div class="specification-page__icon" aria-hidden="true">✦</div>
        <h2>Страница ТЗ готовится</h2>
        <p>Здесь появится структурированное техническое задание по итогам встречи. Редактор, транскрипция и плеер будут добавлены на следующем этапе.</p>
      </section>
      <RouterLink class="specification-page__back" :to="{ name: 'projects' }">← Вернуться к проектам</RouterLink>
    </main>
  </div>
</template>

<style scoped lang="sass">
.specification-page
  min-height: 100vh
  background: var(--color-background)

  &__main
    width: min(100%, 760px)
    margin: 0 auto
    padding: 48px 24px
    text-align: center

  &__eyebrow
    margin: 0 0 8px
    color: var(--color-muted)
    font-size: 12px
    font-weight: 600
    letter-spacing: .8px
    text-transform: uppercase

  h1
    margin: 0
    font-size: 30px

  &__placeholder
    display: grid
    justify-items: center
    gap: 10px
    margin-top: 40px
    padding: 56px 32px
    border: 1px dashed #d1d5db
    border-radius: 16px
    background: var(--color-surface)

    h2, p
      margin: 0

    h2
      font-size: 20px

    p
      max-width: 500px
      color: var(--color-muted)
      font-size: 14px
      line-height: 1.6

  &__icon
    display: grid
    width: 48px
    height: 48px
    place-items: center
    border-radius: 50%
    background: var(--color-accent-soft)
    color: var(--color-accent)
    font-size: 24px

  &__back
    display: inline-block
    margin-top: 24px
    color: var(--color-accent-strong)
    font-size: 14px
    font-weight: 600
</style>
