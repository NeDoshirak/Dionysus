<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { verifyEmail } from '@/entities/session'
import { BaseButton, StatusMessage } from '@/shared/ui'

const props = defineProps({ email: { type: String, default: '' } })

const router = useRouter()
const digits = ref(['', '', '', '', '', ''])
const state = ref('initial')
const serverError = ref('')
const code = computed(() => digits.value.join(''))

function setDigit(index, event) {
  digits.value[index] = event.target.value.replace(/\D/g, '').slice(-1)
}

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
  <form class="verify-email-form" novalidate @submit.prevent="submitForm">
    <p class="verify-email-form__brand">SpecScribe</p>
    <div class="verify-email-form__badge" aria-hidden="true">@</div>
    <h1 class="verify-email-form__title">Подтвердите email</h1>
    <p class="verify-email-form__subtitle">Отправили 6-значный код на <strong>{{ email || 'ваш email' }}</strong></p>
    <StatusMessage v-if="state === 'server-error'" state="error">{{ serverError }}</StatusMessage>
    <StatusMessage v-if="state === 'success'" state="success">Email подтверждён.</StatusMessage>
    <div class="verify-email-form__digits" aria-label="Код подтверждения">
      <input v-for="(_, index) in digits" :key="index" :value="digits[index]" class="verify-email-form__digit" inputmode="numeric" maxlength="1" :aria-label="`Цифра кода ${index + 1}`" @input="setDigit(index, $event)">
    </div>
    <p v-if="state === 'validation'" class="verify-email-form__validation" role="alert">Введите все 6 цифр кода.</p>
    <p class="verify-email-form__resend">Отправить повторно через 55 с</p>
    <BaseButton class="verify-email-form__submit" type="submit" :loading="state === 'submitting'">Подтвердить email</BaseButton>
    <p class="verify-email-form__footer">Неверный адрес? <RouterLink :to="{ name: 'sign-up' }">Зарегистрироваться заново</RouterLink></p>
  </form>
</template>

<style scoped lang="sass">
.verify-email-form
  display: grid
  width: min(100%, 340px)
  margin: 0 auto
  text-align: center

  &__brand
    margin: 0
    color: var(--color-accent)
    font-size: 22px
    font-weight: 700
    letter-spacing: -.5px

  &__badge
    display: grid
    width: 48px
    height: 48px
    place-items: center
    margin: 28px auto 0
    border: 1px solid #fecaca
    border-radius: 24px
    background: var(--color-accent-soft)
    color: var(--color-accent-strong)
    font-weight: 700

  &__title
    margin: 16px 0 0
    font-size: 28px
    line-height: 1.5
    letter-spacing: -.5px

  &__subtitle
    margin: 6px 0 0
    color: var(--color-muted)
    font-size: 15px
    line-height: 1.5

    strong
      display: block
      color: #374151

  &__digits
    display: grid
    grid-template-columns: repeat(6, minmax(0, 1fr))
    gap: 10px
    margin-top: 28px

  &__digit
    width: 100%
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

  &__resend
    margin: 8px 0 0
    color: #9ca3af
    font-size: 13px

  &__submit
    width: 100%
    margin-top: 24px

  &__footer
    margin: 20px 0 0
    color: var(--color-muted)
    font-size: 14px

    a
      color: var(--color-accent-strong)
      font-weight: 600

@media (max-width: 390px)
  .verify-email-form
    &__digits
      gap: 6px
</style>
