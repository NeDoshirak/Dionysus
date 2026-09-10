<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { signIn } from '@/entities/session'
import { validateEmail, validatePassword } from '@/shared/lib/validation'
import { AuthFormFrame, BaseButton, BaseInput, StatusMessage } from '@/shared/ui'

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
    await signIn({ email: email.value.trim(), password: password.value })
    state.value = 'success'
    await router.push({ name: 'projects' })
  } catch (error) {
    if (error.code === 'unconfirmed-email') {
      await router.push({ name: 'verify-email', query: { email: email.value.trim() } })
      return
    }
    state.value = 'server-error'
    errorMessage.value = error.status === 401 || error.code === 'invalid-credentials'
      ? 'Неверный email или пароль.'
      : 'Не удалось войти. Попробуйте ещё раз.'
  }
}
</script>

<template>
  <AuthFormFrame class="sign-in-form" title="Войти в аккаунт" subtitle="Продолжите работу над вашими встречами." @submit="submitForm">
    <StatusMessage v-if="state === 'server-error'" state="error">{{ errorMessage }}</StatusMessage>
    <BaseInput v-model="email" class="sign-in-form__field" label="Рабочий email" type="email" autocomplete="email" placeholder="you@company.com" :error="emailError" />
    <BaseInput v-model="password" class="sign-in-form__field" label="Пароль" type="password" autocomplete="current-password" placeholder="Введите пароль" :error="state === 'validation' && password && !validatePassword(password).isValid ? 'Проверьте пароль.' : ''" />
    <RouterLink class="sign-in-form__forgot" :to="{ name: 'reset-password' }">Забыли пароль?</RouterLink>
    <BaseButton class="sign-in-form__submit" type="submit" :loading="state === 'submitting'">Войти</BaseButton>
    <template #footer>Нет аккаунта? <RouterLink :to="{ name: 'sign-up' }">Зарегистрироваться</RouterLink></template>
  </AuthFormFrame>
</template>

<style scoped lang="sass">
.sign-in-form
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

</style>
