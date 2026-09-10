<script setup>
defineProps({ state: { type: String, default: 'info' } })
</script>

<template>
  <div
    class="status-message"
    :class="`status-message--${state}`"
    :role="state === 'error' ? 'alert' : 'status'"
    :aria-busy="state === 'loading' || undefined"
  ><slot /></div>
</template>

<style scoped lang="sass">
.status-message
  padding: 12px 16px
  border: 1px solid var(--color-border)
  border-radius: var(--radius-sm)
  background: var(--color-background)
  color: var(--color-muted)
  font-size: 14px
  line-height: 1.6
  overflow-wrap: anywhere

  &--error
    border-color: var(--color-danger)
    background: var(--color-accent-soft)
    color: var(--color-danger)

  &--success
    color: var(--color-success)
</style>
