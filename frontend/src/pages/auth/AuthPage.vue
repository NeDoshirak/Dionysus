<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { SignUpForm } from '@/features/sign-up'
import { SignInForm } from '@/features/sign-in'
import { ResetPasswordForm } from '@/features/reset-password'
import { VerifyEmailForm } from '@/features/verify-email'
import { BaseButton } from '@/shared/ui'
import { AuthLayout } from '@/widgets/auth-layout'

const route = useRoute()
const email = computed(() => route.query.email || '')
</script>

<template>
  <AuthLayout>
    <SignUpForm v-if="route.name === 'sign-up'" />
    <SignInForm v-else-if="route.name === 'sign-in'" />
    <VerifyEmailForm v-else-if="route.name === 'verify-email'" :email="email" />
    <ResetPasswordForm v-else-if="route.name === 'reset-password'" :email="email" :step="route.query.step" />
    <section v-else class="auth-page__success" aria-labelledby="confirmation-title">
      <p class="auth-page__brand">SpecScribe</p>
      <div class="auth-page__success-mark" aria-hidden="true">✓</div>
      <h1 id="confirmation-title">Email подтверждён</h1>
      <p>Аккаунт активирован. Добро пожаловать в SpecScribe!</p>
      <BaseButton class="auth-page__projects-link" :to="{ name: 'projects' }">Перейти к проектам</BaseButton>
    </section>
  </AuthLayout>
</template>

<style scoped lang="sass">
.auth-page
  &__success
    width: min(100%, 381px)
    margin: 0 auto
    text-align: center

    h1
      margin: 20px 0 0
      font-size: 28px
      line-height: 34px
      letter-spacing: -.5px

    p:not(.auth-page__brand)
      margin: 10px 0 0
      color: var(--color-muted)
      font-size: 15px
      line-height: 22px

  &__brand
    margin: 0
    color: var(--color-accent)
    font-size: 22px
    font-weight: 700
    letter-spacing: -.5px

  &__success-mark
    display: grid
    width: 56px
    height: 56px
    place-items: center
    margin: 44px auto 0
    border: 1px solid #a7f3d0
    border-radius: 28px
    background: #ecfdf5
    color: var(--color-success)
    font-size: 28px
    font-weight: 700

  &__projects-link
    display: inline-flex
    width: 100%
    min-height: 48px
    align-items: center
    justify-content: center
    margin-top: 28px
    border-radius: 8px
    background: var(--color-accent-strong)
    color: var(--color-surface)
    font-weight: 600

    &:hover
      background: var(--color-accent-hover)
</style>
