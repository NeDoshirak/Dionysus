<script setup>
import { onBeforeUnmount, ref, useId, watch } from 'vue'

const props = defineProps({
  open: Boolean,
  title: { type: String, required: true },
})
defineEmits(['close'])
const dialog = ref(null)
const titleId = useId()
const isClosing = ref(false)
let closeTimer

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

function clearCloseTimer() {
  if (closeTimer) window.clearTimeout(closeTimer)
  closeTimer = undefined
}

function finishClosing() {
  clearCloseTimer()
  isClosing.value = false
  closeDialog()
}

function startClosing() {
  if (!dialog.value?.open || isClosing.value) return

  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
    finishClosing()
    return
  }

  isClosing.value = true
  closeTimer = window.setTimeout(finishClosing, 220)
}

function onAnimationEnd(event) {
  if (isClosing.value && event.animationName === 'base-dialog-fade-out') finishClosing()
}

watch(() => props.open, (open) => {
  if (open) {
    clearCloseTimer()
    isClosing.value = false
    if (!dialog.value?.open) showDialog()
  } else if (dialog.value?.open) {
    startClosing()
  }
}, { flush: 'post' })

watch(dialog, (element) => {
  if (element && props.open) showDialog()
})

onBeforeUnmount(() => {
  clearCloseTimer()
  if (dialog.value?.open) closeDialog()
})
</script>

<template>
  <dialog
    ref="dialog"
    class="base-dialog base-dialog--fade-in"
    :class="{ 'base-dialog--closing': isClosing }"
    :aria-labelledby="titleId"
    @cancel.prevent="$emit('close')"
    @animationend="onAnimationEnd"
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
  padding: var(--space-7)
  border: 1px solid var(--color-border)
  border-radius: var(--radius-lg)
  background: var(--color-surface)
  color: var(--color-text)
  box-shadow: 0 24px 80px rgb(17 24 39 / 20%)
  overflow: auto
  animation: base-dialog-fade-in 180ms ease-out

  &::backdrop
    background: rgb(17 24 39 / 55%)
    animation: base-dialog-backdrop-fade-in 180ms ease-out

  &--closing
    animation: base-dialog-fade-out 180ms ease-in forwards

    &::backdrop
      animation: base-dialog-backdrop-fade-out 180ms ease-in forwards

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
    border-radius: var(--radius-sm)
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
    padding: var(--space-5)

@media (prefers-reduced-motion: reduce)
  .base-dialog, .base-dialog::backdrop
    animation: none

@keyframes base-dialog-fade-in
  from
    opacity: 0

  to
    opacity: 1

@keyframes base-dialog-backdrop-fade-in
  from
    background: transparent

  to
    background: rgb(17 24 39 / 55%)

@keyframes base-dialog-fade-out
  from
    opacity: 1

  to
    opacity: 0

@keyframes base-dialog-backdrop-fade-out
  from
    background: rgb(17 24 39 / 55%)

  to
    background: transparent
</style>
