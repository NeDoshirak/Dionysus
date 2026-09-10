<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { signUp } from '@/entities/session'
import { validatePassword } from '@/shared/lib/validation'
import { BaseButton, BaseInput, StatusMessage } from '@/shared/ui'

const router = useRouter()
const email = ref('')
const password = ref('')
const termsAccepted = ref(false)
const state = ref('initial')
const serverError = ref('')
const showPassword = ref(false)

const passwordError = computed(() => {
  if (state.value !== 'validation' || validatePassword(password.value).isValid) return ''
  return 'Пароль должен содержать не менее 8 символов, заглавную букву и цифру.'
})

async function submitForm() {
  serverError.value = ''
  const passwordValidation = validatePassword(password.value)

  if (!email.value || !passwordValidation.isValid || !termsAccepted.value) {
    state.value = 'validation'
    return
  }

  state.value = 'submitting'
  try {
    const result = await signUp({ email: email.value, password: password.value })
    state.value = 'success'
    await router.push({ name: 'verify-email', query: { email: result.email } })
  } catch {
    state.value = 'server-error'
    serverError.value = 'Не удалось создать аккаунт. Попробуйте ещё раз.'
  }
}
</script>

<template>
  <form class="sign-up-form" novalidate @submit.prevent="submitForm">
    <p class="sign-up-form__brand">SpecScribe</p>
    <h1 class="sign-up-form__title">Создать аккаунт</h1>
    <p class="sign-up-form__subtitle">Бесплатно. Без кредитной карты.</p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ serverError }}</StatusMessage>
    <StatusMessage v-if="state === 'success'" state="success">Аккаунт создан. Проверьте email.</StatusMessage>
    <BaseInput v-model="email" class="sign-up-form__field" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="state === 'validation' && !email ? 'Введите email.' : ''" />
    <div class="sign-up-form__password">
      <BaseInput v-model="password" label="Пароль" :type="showPassword ? 'text' : 'password'" autocomplete="new-password" placeholder="Минимум 8 символов" :error="passwordError" />
      <button class="sign-up-form__password-toggle" type="button" @click="showPassword = !showPassword">{{ showPassword ? 'Скрыть' : 'Показать' }}</button>
    </div>
    <label class="sign-up-form__terms">
      <input v-model="termsAccepted" type="checkbox">
      <span>Я принимаю <a href="#terms">условия использования</a> и <a href="#privacy">политику конфиденциальности</a></span>
    </label>
    <p v-if="state === 'validation' && !termsAccepted" class="sign-up-form__validation" role="alert">Примите условия использования.</p>
    <BaseButton class="sign-up-form__submit" type="submit" :loading="state === 'submitting'">Создать аккаунт</BaseButton>
    <p class="sign-up-form__footer">Уже есть аккаунт? <RouterLink :to="{ name: 'sign-in' }">Войти</RouterLink></p>
  </form>
</template>

<style scoped lang="sass">
.sign-up-form
  display: grid
  width: min(100%, 301px)
  margin: 0 auto

  &__brand, &__title, &__subtitle, &__footer
    text-align: center

  &__brand
    margin: 0
    color: var(--color-accent)
    font-size: 22px
    font-weight: 700
    letter-spacing: -.5px

  &__title
    margin: 28px 0 0
    font-size: 28px
    line-height: 1.5
    letter-spacing: -.5px

  &__subtitle
    margin: 6px 0 0
    color: var(--color-muted)
    font-size: 15px

  &__field
    margin-top: 28px

  &__password
    position: relative
    margin-top: 18px

  &__password-toggle
    position: absolute
    right: 10px
    top: 34px
    border: 0
    background: transparent
    color: var(--color-accent-strong)
    font-size: 13px
    cursor: pointer

  &__terms
    display: flex
    gap: 8px
    margin-top: 18px
    color: #374151
    font-size: 14px
    line-height: 1.5

    input
      width: 18px
      height: 18px
      margin: 1px 0 0
      accent-color: var(--color-accent-strong)

    a
      color: var(--color-accent-strong)
      font-weight: 600

  &__validation
    margin: 8px 0 0
    color: var(--color-danger)
    font-size: 14px

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
