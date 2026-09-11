<script setup>
import { BaseButton, BaseDialog } from '@/shared/ui'

defineProps({
  open: Boolean,
  title: { type: String, required: true },
  message: { type: String, required: true },
  confirmLabel: { type: String, default: 'Подтвердить' },
})
const emit = defineEmits(['confirm', 'close'])
</script>

<template>
  <BaseDialog :open="open" :title="title" @close="emit('close')">
    <div class="specification-confirm-dialog">
      <p class="specification-confirm-dialog__message">{{ message }}</p>
      <div class="specification-confirm-dialog__actions">
        <BaseButton type="button" variant="outline" @click="emit('close')">Отмена</BaseButton>
        <BaseButton type="button" @click="emit('confirm')">{{ confirmLabel }}</BaseButton>
      </div>
    </div>
  </BaseDialog>
</template>

<style scoped lang="sass">
.specification-confirm-dialog
  display: grid
  gap: 24px

  &__message
    margin: 0
    color: var(--color-muted)
    line-height: 1.6

  &__actions
    display: flex
    justify-content: flex-end
    gap: 12px

@media (max-width: 480px)
  .specification-confirm-dialog__actions
    flex-direction: column-reverse

    :deep(.base-button)
      width: 100%
</style>
