<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { signUp } from '@/entities/session'
import { validatePassword } from '@/shared/lib/validation'
import { AuthFormFrame, BaseButton, BaseInput, StatusMessage } from '@/shared/ui'

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
  <AuthFormFrame class="sign-up-form" title="Создать аккаунт" subtitle="Бесплатно. Без кредитной карты." @submit="submitForm">
    <StatusMessage v-if="state === 'server-error'" state="error">{{ serverError }}</StatusMessage>
    <StatusMessage v-if="state === 'success'" state="success">Аккаунт создан. Проверьте email.</StatusMessage>
    <div class="sign-up-form__fields">
      <BaseInput v-model="email" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="state === 'validation' && !email ? 'Введите email.' : ''" />
      <BaseInput
        v-model="password"
        label="Пароль"
        :type="showPassword ? 'text' : 'password'"
        autocomplete="new-password"
        placeholder="Минимум 8 символов"
        :error="passwordError"
      >
        <template #trailing>
          <button class="sign-up-form__password-toggle" type="button" :aria-label="showPassword ? 'Скрыть пароль' : 'Показать пароль'" @click="showPassword = !showPassword">{{ showPassword ? 'Скрыть' : 'Показать' }}</button>
        </template>
      </BaseInput>
    </div>
    <label class="sign-up-form__terms">
      <input v-model="termsAccepted" type="checkbox">
      <span>Я принимаю <a href="#terms">условия использования</a> и <a href="#privacy">политику конфиденциальности</a></span>
    </label>
    <p v-if="state === 'validation' && !termsAccepted" class="sign-up-form__validation" role="alert">Примите условия использования.</p>
    <BaseButton class="sign-up-form__submit" type="submit" :loading="state === 'submitting'">Создать аккаунт</BaseButton>
    <template #footer>Уже есть аккаунт? <RouterLink :to="{ name: 'sign-in' }">Войти</RouterLink></template>
  </AuthFormFrame>
</template>

<style scoped lang="sass">
.sign-up-form
  &__fields
    display: grid
    gap: 18px
    margin-top: var(--space-7)

  &__password-toggle
    border: 0
    background: transparent
    color: var(--color-accent-strong)
    font-size: 13px
    cursor: pointer

  &__terms
    display: flex
    gap: 8px
    margin-top: 18px
    color: var(--color-text-secondary)
    font-size: 14px
    line-height: 21px

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

</style>
