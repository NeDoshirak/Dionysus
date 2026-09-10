<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { confirmPasswordResetLocal, requestPasswordResetLocal } from '@/shared/api/local-adapters'
import { validatePassword } from '@/shared/lib/validation'
import { BaseButton, BaseInput, StatusMessage } from '@/shared/ui'

const props = defineProps({ email: { type: String, default: '' }, step: { type: String, default: '' } })
const route = useRoute()
const router = useRouter()
const email = ref(props.email || route.query.email || '')
const password = ref('')
const code = ref('')
const state = ref('initial')
const errorMessage = ref('')
const seconds = ref(55)
let timer

const codeStep = computed(() => props.step === 'code' || route.query.step === 'code' || state.value === 'code')

function startTimer() {
  seconds.value = 55
  clearInterval(timer)
  timer = setInterval(() => {
    if (seconds.value > 0) seconds.value -= 1
    if (seconds.value === 0) clearInterval(timer)
  }, 1000)
}

async function requestCode() {
  errorMessage.value = ''
  if (!email.value.trim()) {
    state.value = 'validation'
    return
  }
  state.value = 'submitting'
  try {
    const result = await requestPasswordResetLocal({ email: email.value.trim() })
    email.value = result.email
    state.value = 'code'
    startTimer()
    await router.push({ name: 'reset-password', query: { step: 'code', email: email.value } })
  } catch (error) {
    state.value = 'server-error'
    errorMessage.value = error.code === 'validation' ? 'Введите корректный email.' : 'Не удалось отправить код. Попробуйте ещё раз.'
  }
}

async function confirmCode() {
  errorMessage.value = ''
  if (!/^\d{6}$/.test(code.value) || !validatePassword(password.value).isValid) {
    state.value = 'validation'
    return
  }
  state.value = 'submitting'
  try {
    await confirmPasswordResetLocal({ email: email.value, code: code.value, password: password.value })
    state.value = 'success'
  } catch {
    state.value = 'server-error'
    errorMessage.value = 'Не удалось сбросить пароль. Проверьте код и попробуйте ещё раз.'
  }
}

onMounted(() => { if (codeStep.value) startTimer() })
onUnmounted(() => clearInterval(timer))
</script>

<template>
  <form v-if="!codeStep" class="reset-password-form" novalidate @submit.prevent="requestCode">
    <p class="reset-password-form__brand">SpecScribe</p>
    <h1 class="reset-password-form__title">Восстановить пароль</h1>
    <p class="reset-password-form__subtitle">Отправим код подтверждения на ваш email.</p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <BaseInput v-model="email" class="reset-password-form__field" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="state === 'validation' ? 'Введите email.' : ''" />
    <BaseButton class="reset-password-form__submit" type="submit" :loading="state === 'submitting'">Отправить код</BaseButton>
    <p class="reset-password-form__footer"><RouterLink :to="{ name: 'sign-in' }">Вернуться ко входу</RouterLink></p>
  </form>
  <form v-else class="reset-password-form" novalidate @submit.prevent="confirmCode">
    <p class="reset-password-form__brand">SpecScribe</p>
    <h1 class="reset-password-form__title">Введите код</h1>
    <p class="reset-password-form__subtitle">Код отправлен на <strong>{{ email }}</strong></p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <BaseInput v-model="code" class="reset-password-form__field" label="Код подтверждения" inputmode="numeric" maxlength="6" placeholder="123456" :error="state === 'validation' && !/^\d{6}$/.test(code) ? 'Введите 6 цифр.' : ''" />
    <BaseInput v-model="password" class="reset-password-form__field" label="Новый пароль" type="password" autocomplete="new-password" placeholder="Минимум 8 символов" :error="state === 'validation' && !validatePassword(password).isValid ? 'Пароль должен содержать не менее 8 символов, заглавную букву и цифру.' : ''" />
    <p v-if="state === 'success'" class="reset-password-form__success" role="status">Пароль изменён. Теперь можно войти.</p>
    <p v-else class="reset-password-form__resend">Отправить повторно через {{ seconds }} с</p>
    <BaseButton class="reset-password-form__submit" type="submit" :loading="state === 'submitting'">Сохранить пароль</BaseButton>
    <p class="reset-password-form__footer"><RouterLink :to="{ name: 'sign-in' }">Вернуться ко входу</RouterLink></p>
  </form>
</template>

<style scoped lang="sass">
.reset-password-form
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
    line-height: 1.5

  &__subtitle
    margin: 6px 0 0
    color: var(--color-muted)
    font-size: 15px

  &__field
    margin-top: 20px

  &__submit
    width: 100%
    margin-top: 20px

  &__resend, &__success
    margin: 10px 0 0
    color: var(--color-muted)
    font-size: 13px
    text-align: center

  &__success
    color: var(--color-success)

  &__footer
    margin: 20px 0 0
    font-size: 14px

    a
      color: var(--color-accent-strong)
      font-weight: 600
</style>
