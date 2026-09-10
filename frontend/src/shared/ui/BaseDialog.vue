<script setup>
import { onBeforeUnmount, ref, useId, watch } from 'vue'

const props = defineProps({
  open: Boolean,
  title: { type: String, required: true },
})
defineEmits(['close'])
const dialog = ref(null)
const titleId = useId()

function showDialog() {
  if (!dialog.value) return
  if (typeof dialog.value.showModal === 'function') dialog.value.showModal()
  else dialog.value.setAttribute('open', '')
}

function closeDialog() {
  if (!dialog.value) return
  if (typeof dialog.value.close === 'function') dialog.value.close()
  else dialog.value.removeAttribute('open')
}

watch(() => props.open, (open) => {
  if (open && !dialog.value?.open) showDialog()
  else if (!open && dialog.value?.open) closeDialog()
}, { flush: 'post' })

watch(dialog, (element) => {
  if (element && props.open) showDialog()
})

onBeforeUnmount(() => {
  if (dialog.value?.open) closeDialog()
})
</script>

<template>
  <dialog
    ref="dialog"
    class="base-dialog"
    :aria-labelledby="titleId"
    @cancel.prevent="$emit('close')"
  >
    <div class="base-dialog__header">
      <h2 :id="titleId" class="base-dialog__title">{{ title }}</h2>
      <button class="base-dialog__close" type="button" aria-label="Закрыть" @click="$emit('close')">×</button>
    </div>
    <div class="base-dialog__content"><slot /></div>
  </dialog>
</template>

<style scoped lang="sass">
.base-dialog
  width: min(560px, calc(100% - 32px))
  max-height: calc(100dvh - 32px)
  padding: 28px
  border: 1px solid var(--color-border)
  border-radius: 16px
  background: var(--color-surface)
  color: var(--color-text)
  box-shadow: 0 24px 80px rgb(17 24 39 / 20%)
  overflow: auto

  &::backdrop
    background: rgb(17 24 39 / 55%)

  &__header
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px
    margin-bottom: 24px

  &__title
    margin: 0
    font-size: 24px
    line-height: 1.3

  &__close
    flex-shrink: 0
    width: 44px
    height: 44px
    padding: 0
    border: 0
    border-radius: 8px
    background: var(--color-background)
    color: var(--color-muted)
    font-size: 28px
    cursor: pointer

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

  &__content
    min-width: 0
    overflow-wrap: anywhere

@media (max-width: 480px)
  .base-dialog
    padding: 20px
</style>
