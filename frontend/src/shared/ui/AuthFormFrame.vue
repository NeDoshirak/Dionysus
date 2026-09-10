<script setup>
defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: '' },
  width: { type: Number, default: 381 },
})
const emit = defineEmits(['submit'])
</script>

<template>
  <form class="auth-form-frame" novalidate :style="{ '--auth-form-width': `${width}px` }" @submit.prevent="emit('submit')">
    <p class="auth-form-frame__brand">SpecScribe</p>
    <slot name="before-heading" />
    <h1 class="auth-form-frame__title">{{ title }}</h1>
    <p v-if="subtitle || $slots.subtitle" class="auth-form-frame__subtitle"><slot name="subtitle">{{ subtitle }}</slot></p>
    <slot />
    <footer v-if="$slots.footer" class="auth-form-frame__footer"><slot name="footer" /></footer>
  </form>
</template>

<style scoped lang="sass">
.auth-form-frame
  display: grid
  width: min(100%, var(--auth-form-width))
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
    margin: var(--space-7) 0 0
    font-size: 28px
    line-height: 34px
    letter-spacing: -.5px

  &__subtitle
    margin: var(--space-2) 0 0
    color: var(--color-muted)
    font-size: 15px
    line-height: 22px

  &__footer
    margin: var(--space-5) 0 0
    color: var(--color-muted)
    font-size: 14px

    :deep(a)
      color: var(--color-accent-strong)
      font-weight: 600
</style>
