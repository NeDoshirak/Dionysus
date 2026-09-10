<script setup>
import { RouterLink } from 'vue-router'

defineProps({
  to: { type: [String, Object], default: null },
  variant: { type: String, default: 'primary' },
  type: { type: String, default: 'button' },
  disabled: Boolean,
  loading: Boolean,
})
</script>

<template>
  <RouterLink
    v-if="to && !disabled && !loading"
    :to="to"
    class="base-button"
    :class="`base-button--${variant}`"
  ><slot /></RouterLink>
  <button
    v-else
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
    class="base-button"
    :class="`base-button--${variant}`"
  ><slot /></button>
</template>

<style scoped lang="sass">
.base-button
  display: inline-flex
  align-items: center
  justify-content: center
  gap: 8px
  min-height: 48px
  padding: 11px 23px
  border: 1px solid transparent
  border-radius: var(--radius-sm)
  background: var(--color-accent-strong)
  color: var(--color-surface)
  font: inherit
  font-weight: 600
  line-height: 1.5
  text-align: center
  text-decoration: none
  cursor: pointer
  transition: background-color 150ms ease

  &:hover:not(:disabled)
    background: var(--color-accent-hover)

  &:focus-visible
    outline: 3px solid var(--color-focus)
    outline-offset: 4px

  &:disabled
    opacity: .55
    cursor: not-allowed

  &--outline
    border-color: var(--color-accent-strong)
    background: transparent
    color: var(--color-accent-strong)

    &:hover:not(:disabled)
      background: var(--color-accent-soft)

  &--text
    padding-inline: 0
    background: transparent
    color: inherit

    &:hover:not(:disabled)
      background: transparent
      text-decoration: underline
      text-underline-offset: 4px

@media (prefers-reduced-motion: reduce)
  .base-button
    transition: none
</style>
