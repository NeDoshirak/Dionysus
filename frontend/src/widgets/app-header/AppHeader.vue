<script setup>
import { RouterLink } from 'vue-router'
</script>

<template>
  <header class="app-header">
    <div class="app-header__inner">
      <RouterLink class="app-header__brand" to="/projects">SpecScribe</RouterLink>
      <RouterLink class="app-header__profile" to="/profile">
        <span class="app-header__avatar">АИ</span>
        <span>Александр И.</span>
        <span aria-hidden="true">⌄</span>
      </RouterLink>
    </div>
  </header>
</template>

<style scoped lang="sass">
.app-header
  border-bottom: 1px solid var(--color-border)
  background: rgba(255, 255, 255, .92)
  backdrop-filter: blur(10px)

  &__inner
    display: flex
    align-items: center
    justify-content: space-between
    max-width: 960px
    min-height: 60px
    margin: 0 auto
    padding: 0 32px

  &__brand
    color: var(--color-accent)
    font-size: 20px
    font-weight: 700
    text-decoration: none

  &__profile
    display: inline-flex
    align-items: center
    gap: 8px
    padding: 6px 12px 6px 6px
    border: 1px solid var(--color-border)
    border-radius: 100px
    color: var(--color-text)
    font-size: 14px
    text-decoration: none

  &__avatar
    display: grid
    width: 28px
    height: 28px
    place-items: center
    border-radius: 50%
    background: var(--color-accent)
    color: white
    font-size: 12px
    font-weight: 700
</style>
