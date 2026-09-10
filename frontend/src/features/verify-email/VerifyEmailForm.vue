<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { verifyEmail } from '@/entities/session'
import { AuthFormFrame, BaseButton, CodeInput, StatusMessage } from '@/shared/ui'

const props = defineProps({ email: { type: String, default: '' } })

const router = useRouter()
const code = ref('')
const state = ref('initial')
const serverError = ref('')
const displayedEmail = computed(() => {
  if (!props.email) return 'ваш email'
  const [localPart, domain] = props.email.split('@')
  if (!domain) return props.email
  return `${localPart.slice(0, 1)}***@${domain}`
})

async function submitForm() {
  serverError.value = ''
  if (!/^\d{6}$/.test(code.value)) {
    state.value = 'validation'
    return
  }

  state.value = 'submitting'
  try {
    await verifyEmail({ email: props.email, code: code.value })
    state.value = 'success'
    await router.push({ name: 'confirmation-success' })
  } catch {
    state.value = 'server-error'
    serverError.value = 'Не удалось подтвердить email. Проверьте код и попробуйте ещё раз.'
  }
}
</script>

<template>
  <AuthFormFrame class="verify-email-form" title="Подтвердите email" :width="420" @submit="submitForm">
    <template #before-heading><div class="verify-email-form__badge" aria-hidden="true">@</div></template>
    <template #subtitle>Отправили 6-значный код на <strong class="verify-email-form__email">{{ displayedEmail }}</strong></template>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ serverError }}</StatusMessage>
    <StatusMessage v-if="state === 'success'" state="success">Email подтверждён.</StatusMessage>
    <CodeInput v-model="code" class="verify-email-form__digits" />
    <p v-if="state === 'validation'" class="verify-email-form__validation" role="alert">Введите все 6 цифр кода.</p>
    <p class="verify-email-form__resend">Отправить повторно через 55 с</p>
    <BaseButton class="verify-email-form__submit" type="submit" :loading="state === 'submitting'">Подтвердить email</BaseButton>
    <template #footer>Неверный адрес? <RouterLink :to="{ name: 'sign-up' }">Зарегистрироваться заново</RouterLink></template>
  </AuthFormFrame>
</template>

<style scoped lang="sass">
.verify-email-form
  &__badge
    display: grid
    width: 48px
    height: 48px
    place-items: center
    margin: var(--space-7) auto 0
    border: 1px solid #fecaca
    border-radius: 24px
    background: var(--color-accent-soft)
    color: var(--color-accent-strong)
    font-weight: 700

  &__email
    display: block
    color: var(--color-text-secondary)

  &__digits
    margin-top: 28px

  &__validation
    margin: 8px 0 0
    color: var(--color-danger)
    font-size: 14px
    line-height: 20px

  &__resend
    margin: 8px 0 0
    color: var(--color-subtle)
    font-size: 13px

  &__submit
    width: 100%
    margin-top: 24px

</style>
