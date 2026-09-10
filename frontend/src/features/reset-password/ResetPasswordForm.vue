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
const digits = ref(['', '', '', '', '', ''])
const state = ref('initial')
const errorMessage = ref('')
const seconds = ref(55)
const inputRefs = ref([])
let timer

const codeStep = computed(() => props.step === 'code' || route.query.step === 'code' || state.value === 'code')
const displayedEmail = computed(() => {
  const [localPart, domain] = email.value.split('@')
  if (!domain) return email.value
  return `${localPart.slice(0, 1)}***@${domain}`
})

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
  code.value = digits.value.join('')
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

function setDigit(index, event) {
  digits.value[index] = event.target.value.replace(/\D/g, '').slice(-1)
  if (digits.value[index] && index < digits.value.length - 1) inputRefs.value[index + 1]?.focus()
}

function setInputRef(element, index) {
  if (element) inputRefs.value[index] = element
}

function handleKeydown(index, event) {
  if (event.key === 'Backspace' && !digits.value[index] && index > 0) {
    event.preventDefault()
    inputRefs.value[index - 1]?.focus()
  }
}

function handlePaste(index, event) {
  const pastedDigits = event.clipboardData?.getData('text').replace(/\D/g, '').slice(0, digits.value.length - index)
  if (!pastedDigits) return
  event.preventDefault()
  for (const [offset, digit] of [...pastedDigits].entries()) digits.value[index + offset] = digit
  inputRefs.value[index + pastedDigits.length - 1]?.focus()
}

async function changeEmail() {
  clearInterval(timer)
  await router.push({ name: 'reset-password' })
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
    <p class="reset-password-form__subtitle">Код отправлен на <strong>{{ displayedEmail }}</strong></p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <div class="reset-password-form__code-group" aria-label="Код подтверждения">
      <input v-for="(_, index) in digits" :key="index" :ref="(element) => setInputRef(element, index)" :value="digits[index]" class="reset-password-form__digit" inputmode="numeric" maxlength="1" :aria-label="`Цифра кода ${index + 1}`" @input="setDigit(index, $event)" @keydown="handleKeydown(index, $event)" @paste="handlePaste(index, $event)">
    </div>
    <p v-if="state === 'validation' && !/^\d{6}$/.test(digits.join(''))" class="reset-password-form__validation" role="alert">Введите 6 цифр.</p>
    <BaseInput v-model="password" class="reset-password-form__field" label="Новый пароль" type="password" autocomplete="new-password" placeholder="Минимум 8 символов" :error="state === 'validation' && !validatePassword(password).isValid ? 'Пароль должен содержать не менее 8 символов, заглавную букву и цифру.' : ''" />
    <p v-if="state === 'success'" class="reset-password-form__success" role="status">Пароль изменён. Теперь можно войти.</p>
    <button v-if="seconds === 0" class="reset-password-form__resend reset-password-form__resend--active" type="button" @click="requestCode">Отправить код повторно</button>
    <p v-else class="reset-password-form__resend">Отправить повторно через {{ seconds }} с</p>
    <BaseButton class="reset-password-form__submit" type="submit" :loading="state === 'submitting'">Сохранить пароль</BaseButton>
    <button class="reset-password-form__change-email" type="button" @click="changeEmail">Изменить email</button>
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
    line-height: 34px

  &__subtitle
    margin: 6px 0 0
    color: var(--color-muted)
    font-size: 15px
    line-height: 22px

  &__field
    margin-top: 20px

  &__code-group
    display: grid
    grid-template-columns: repeat(6, 52px)
    gap: 10px
    margin-top: 20px

  &__digit
    width: 52px
    height: 60px
    border: 1px solid var(--color-border)
    border-radius: 10px
    background: var(--color-surface)
    color: var(--color-text)
    font-size: 24px
    font-weight: 600
    text-align: center

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

  &__validation
    margin: 8px 0 0
    color: var(--color-danger)
    font-size: 14px

  &__submit
    width: 100%
    margin-top: 20px

  &__resend, &__success
    margin: 10px 0 0
    color: var(--color-muted)
    font-size: 13px
    text-align: center

  &__resend
    border: 0
    background: transparent
    cursor: default

    &--active
      color: var(--color-accent-strong)
      cursor: pointer

  &__change-email
    margin: 16px auto 0
    border: 0
    background: transparent
    color: var(--color-accent-strong)
    font-size: 14px
    font-weight: 600
    cursor: pointer

  &__success
    color: var(--color-success)

  &__footer
    margin: 20px 0 0
    font-size: 14px

    a
      color: var(--color-accent-strong)
      font-weight: 600
</style>
