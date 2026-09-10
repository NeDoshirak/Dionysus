<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { getSession } from '@/entities/session'
import { ProfileForm, SignOutButton } from '@/features/profile'
import { AppHeader } from '@/widgets/app-header'

const session = getSession()
const email = session?.email || ''
const initials = computed(() => email.charAt(0).toUpperCase() || '—')
</script>

<template>
  <div class="profile-page">
    <AppHeader />
    <main class="profile-page__main">
      <div class="profile-page__intro">
        <div class="profile-page__avatar">{{ initials }}</div>
        <div><h1>Профиль</h1><p>{{ email }}</p></div>
        <RouterLink class="profile-page__back" :to="{ name: 'projects' }">← Назад к проектам</RouterLink>
      </div>
      <ProfileForm :email="email" />
      <section class="profile-page__sign-out"><h2>Сессия</h2><p>Выйдите из аккаунта на этом устройстве.</p><SignOutButton /></section>
    </main>
  </div>
</template>

<style scoped lang="sass">
.profile-page
  min-height: 100vh
  background: var(--color-background)

  &__main
    width: min(100%, 620px)
    margin: 0 auto
    padding: 36px 24px 80px

  &__intro
    display: flex
    align-items: center
    gap: 16px
    margin-bottom: 32px

    h1, p
      margin: 0

    h1
      font-size: 18px

    p
      margin-top: 2px
      color: var(--color-muted)
      font-size: 13px

  &__avatar
    display: grid
    width: 56px
    height: 56px
    place-items: center
    border-radius: 50%
    background: var(--color-accent)
    color: white
    font-size: 20px
    font-weight: 700

  &__back
    margin-left: auto
    padding: 8px 14px
    border: 1px solid var(--color-border)
    border-radius: 8px
    color: var(--color-text-secondary)
    font-size: 13px

  &__sign-out
    display: grid
    gap: 12px
    margin-top: 16px
    padding: 20px 24px
    border: 1px solid var(--color-border)
    border-radius: 12px
    background: var(--color-surface)

    h2, p
      margin: 0

    h2
      font-size: 15px

    p
      color: var(--color-muted)
      font-size: 13px

@media (max-width: 680px)
  .profile-page__intro
    align-items: flex-start
    flex-wrap: wrap

  .profile-page__back
    width: 100%
    margin-left: 72px
    text-align: center
</style>
