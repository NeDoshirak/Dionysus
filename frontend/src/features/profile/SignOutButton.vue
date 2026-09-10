<script setup>
import { useRouter } from 'vue-router'
import { signOut as endSession } from '@/entities/session'

const router = useRouter()

async function signOut() {
  await endSession()
  await router.push({ name: 'landing' })
}
</script>

<template><button class="sign-out-button" type="button" @click="signOut">Выйти</button></template>

<style scoped lang="sass">
.sign-out-button
  min-height: 38px
  padding: 8px 20px
  border: 1px solid var(--color-accent-strong)
  border-radius: 8px
  background: transparent
  color: var(--color-accent-strong)
  font: inherit
  font-size: 13px
  font-weight: 600
  cursor: pointer

  &:hover
    background: var(--color-accent-soft)
</style>
