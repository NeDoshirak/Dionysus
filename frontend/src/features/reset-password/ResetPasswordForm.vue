<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { confirmPasswordReset, requestPasswordReset } from '@/entities/session'
import { validatePassword } from '@/shared/lib/validation'
import { AuthFormFrame, BaseButton, BaseInput, CodeInput, StatusMessage } from '@/shared/ui'

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
    const result = await requestPasswordReset({ email: email.value.trim() })
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
    await confirmPasswordReset({ email: email.value, code: code.value, newPassword: password.value })
    state.value = 'success'
  } catch {
    state.value = 'server-error'
    errorMessage.value = 'Не удалось сбросить пароль. Проверьте код и попробуйте ещё раз.'
  }
}

async function changeEmail() {
  clearInterval(timer)
  await router.push({ name: 'reset-password' })
}

onMounted(() => { if (codeStep.value) startTimer() })
onUnmounted(() => clearInterval(timer))
</script>

<template>
  <AuthFormFrame
    class="reset-password-form"
    :title="codeStep ? 'Введите код' : 'Восстановить пароль'"
    :subtitle="codeStep ? '' : 'Отправим код подтверждения на ваш email.'"
    @submit="codeStep ? confirmCode() : requestCode()"
  >
    <template v-if="codeStep" #subtitle>Код отправлен на <strong>{{ displayedEmail }}</strong></template>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <template v-if="!codeStep">
      <BaseInput v-model="email" class="reset-password-form__field" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="state === 'validation' ? 'Введите email.' : ''" />
      <BaseButton class="reset-password-form__submit" type="submit" :loading="state === 'submitting'">Отправить код</BaseButton>
    </template>
    <template v-else>
      <CodeInput v-model="code" class="reset-password-form__code-group" />
      <p v-if="state === 'validation' && !/^\d{6}$/.test(code)" class="reset-password-form__validation" role="alert">Введите 6 цифр.</p>
      <BaseInput v-model="password" class="reset-password-form__field" label="Новый пароль" type="password" autocomplete="new-password" placeholder="Минимум 8 символов" :error="state === 'validation' && !validatePassword(password).isValid ? 'Пароль должен содержать не менее 8 символов, заглавную букву и цифру.' : ''" />
      <p v-if="state === 'success'" class="reset-password-form__success" role="status">Пароль изменён. Теперь можно войти.</p>
      <button v-if="seconds === 0" class="reset-password-form__resend reset-password-form__resend--active" type="button" @click="requestCode">Отправить код повторно</button>
      <p v-else class="reset-password-form__resend">Отправить повторно через {{ seconds }} с</p>
      <BaseButton class="reset-password-form__submit" type="submit" :loading="state === 'submitting'">Сохранить пароль</BaseButton>
      <button class="reset-password-form__change-email" type="button" @click="changeEmail">Изменить email</button>
    </template>
    <template #footer><RouterLink :to="{ name: 'sign-in' }">Вернуться ко входу</RouterLink></template>
  </AuthFormFrame>
</template>

<style scoped lang="sass">
.reset-password-form
  &__field
    margin-top: 20px

  &__code-group
    margin-top: 20px

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

</style>
