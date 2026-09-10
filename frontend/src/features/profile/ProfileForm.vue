<script setup>
import { ref } from 'vue'
import { BaseButton, BaseInput } from '@/shared/ui'

const props = defineProps({ email: { type: String, default: '' } })
const email = ref(props.email)
const currentPassword = ref('')
const newPassword = ref('')
const passwordConfirmation = ref('')
const emailSaved = ref(false)
const passwordSaved = ref(false)

function saveEmail() {
  emailSaved.value = true
}

function savePassword() {
  passwordSaved.value = true
}
</script>

<template>
  <div class="profile-form">
    <form class="profile-form__section" @submit.prevent="saveEmail">
      <div class="profile-form__heading"><h2>Email</h2></div>
      <div class="profile-form__content">
        <BaseInput v-model="email" label="Адрес электронной почты" type="email" autocomplete="email" />
        <div class="profile-form__actions">
          <p>На этот адрес будут приходить уведомления</p>
          <BaseButton type="submit">Сохранить</BaseButton>
        </div>
        <p v-if="emailSaved" class="profile-form__saved" role="status">Изменения сохранены локально</p>
      </div>
    </form>

    <form class="profile-form__section" @submit.prevent="savePassword">
      <div class="profile-form__heading"><h2>Изменение пароля</h2></div>
      <div class="profile-form__content profile-form__content--password">
        <BaseInput v-model="currentPassword" label="Текущий пароль" type="password" autocomplete="current-password" placeholder="Ваш текущий пароль" />
        <BaseInput v-model="newPassword" label="Новый пароль" type="password" autocomplete="new-password" placeholder="Минимум 8 символов" />
        <BaseInput v-model="passwordConfirmation" label="Подтвердите пароль" type="password" autocomplete="new-password-confirmation" placeholder="Повторите новый пароль" />
        <div class="profile-form__actions profile-form__actions--end"><BaseButton type="submit">Сохранить</BaseButton></div>
        <p v-if="passwordSaved" class="profile-form__saved" role="status">Изменения сохранены локально</p>
      </div>
    </form>
  </div>
</template>

<style scoped lang="sass">
.profile-form
  display: grid
  gap: 16px

  &__section
    overflow: hidden
    border: 1px solid var(--color-border)
    border-radius: var(--radius-md)
    background: var(--color-surface)

  &__heading
    padding: 18px 24px
    border-bottom: 1px solid var(--color-border-subtle)

    h2
      margin: 0
      font-size: 15px

  &__content
    display: grid
    gap: 20px
    padding: var(--space-5) var(--space-6)

    &--password
      gap: 18px

  &__actions
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px
    padding-top: 2px

    p, &--end p
      margin: 0
      color: var(--color-subtle)
      font-size: 12px

    &--end
      justify-content: flex-end

  &__saved
    margin: 0
    color: var(--color-success)
    font-size: 12px
</style>
