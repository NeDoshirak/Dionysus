<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { setSession } from '@/entities/session'
import { signInLocal } from '@/shared/api/local-adapters'
import { validateEmail, validatePassword } from '@/shared/lib/validation'
import { BaseButton, BaseInput, StatusMessage } from '@/shared/ui'

const router = useRouter()
const email = ref('')
const password = ref('')
const state = ref('initial')
const errorMessage = ref('')
const emailError = computed(() => {
  if (state.value !== 'validation' || validateEmail(email.value)) return ''
  return email.value.trim() ? 'Введите корректный email.' : 'Введите email.'
})

async function submitForm() {
  errorMessage.value = ''
  if (!validateEmail(email.value) || !validatePassword(password.value).isValid) {
    state.value = 'validation'
    return
  }

  state.value = 'submitting'
  try {
    const session = await signInLocal({ email: email.value.trim(), password: password.value })
    setSession(session)
    state.value = 'success'
    await router.push({ name: 'projects' })
  } catch (error) {
    if (error.code === 'unconfirmed-email') {
      await router.push({ name: 'verify-email', query: { email: email.value.trim() } })
      return
    }
    state.value = 'server-error'
    errorMessage.value = error.code === 'invalid-credentials'
      ? 'Неверный email или пароль.'
      : 'Не удалось войти. Попробуйте ещё раз.'
  }
}
</script>

<template>
  <form class="sign-in-form" novalidate @submit.prevent="submitForm">
    <p class="sign-in-form__brand">SpecScribe</p>
    <h1 class="sign-in-form__title">Войти в аккаунт</h1>
    <p class="sign-in-form__subtitle">Продолжите работу над вашими встречами.</p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <BaseInput v-model="email" class="sign-in-form__field" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="emailError" />
    <BaseInput v-model="password" class="sign-in-form__field" label="Пароль" type="password" autocomplete="current-password" placeholder="Введите пароль" :error="state === 'validation' && password && !validatePassword(password).isValid ? 'Проверьте пароль.' : ''" />
    <RouterLink class="sign-in-form__forgot" :to="{ name: 'reset-password' }">Забыли пароль?</RouterLink>
    <BaseButton class="sign-in-form__submit" type="submit" :loading="state === 'submitting'">Войти</BaseButton>
    <p class="sign-in-form__footer">Нет аккаунта? <RouterLink :to="{ name: 'sign-up' }">Зарегистрироваться</RouterLink></p>
  </form>
</template>

<style scoped lang="sass">
.sign-in-form
  display: grid
  width: min(100%, 381px)
  margin: 0 auto

  &__brand, &__title, &__subtitle, &__footer
    text-align: center

  &__brand
    margin: 0
    color: var(--color-accent)
    font-size: 22px
    font-weight: 700

  &__title
    margin: 28px 0 0
    font-size: 28px
    line-height: 34px

  &__subtitle
    margin: 6px 0 0
    color: var(--color-muted)
    font-size: 15px
    line-height: 22px

  &__field
    margin-top: 20px

  &__forgot
    justify-self: end
    margin-top: 10px
    color: var(--color-accent-strong)
    font-size: 14px
    font-weight: 600

  &__submit
    width: 100%
    margin-top: 20px

  &__footer
    margin: 20px 0 0
    color: var(--color-muted)
    font-size: 14px

    a
      color: var(--color-accent-strong)
      font-weight: 600
</style>
