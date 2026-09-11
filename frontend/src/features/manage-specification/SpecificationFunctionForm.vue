<script setup>
import { ref, watch } from 'vue'
import { BaseButton, BaseDialog, BaseInput } from '@/shared/ui'

const props = defineProps({
  open: Boolean,
  mode: { type: String, default: 'create' },
  sortOrder: { type: Number, default: 0 },
  functionItem: { type: Object, default: null },
  error: { type: String, default: '' },
})
const emit = defineEmits(['save', 'cancel'])

const title = ref('')
const description = ref('')
const descriptionError = ref('')

watch(() => [props.open, props.functionItem, props.mode], () => {
  title.value = props.functionItem?.title || ''
  description.value = props.functionItem?.description || ''
  descriptionError.value = ''
}, { immediate: true })

function submit() {
  descriptionError.value = description.value.trim() ? '' : 'Добавьте описание'
  if (descriptionError.value) return

  emit('save', {
    title: title.value.trim(),
    description: description.value.trim(),
    sortOrder: props.functionItem?.sortOrder ?? props.sortOrder,
    sourceStatementIds: props.functionItem?.sourceStatementIds ? [...props.functionItem.sourceStatementIds] : [],
  })
}
</script>

<template>
  <BaseDialog
    :open="open"
    :title="mode === 'edit' ? 'Редактировать функцию' : 'Новая функция'"
    @close="emit('cancel')"
  >
    <form class="specification-function-form" @submit.prevent="submit">
      <BaseInput
        v-model="title"
        name="title"
        label="Название функции"
        placeholder="Например, Authentication"
      />

      <label class="specification-function-form__field">
        <span class="specification-function-form__label">Описание</span>
        <textarea
          v-model="description"
          class="specification-function-form__control"
          name="description"
          placeholder="Опишите назначение функции"
          :aria-invalid="descriptionError ? 'true' : undefined"
          @input="descriptionError = ''"
        />
        <span v-if="descriptionError" class="specification-function-form__error" role="alert">{{ descriptionError }}</span>
      </label>

      <p v-if="error" class="specification-function-form__request-error" role="alert">{{ error }}</p>

      <div class="specification-function-form__actions">
        <BaseButton type="button" variant="outline" @click="emit('cancel')">Отмена</BaseButton>
        <BaseButton type="submit">Сохранить</BaseButton>
      </div>
    </form>
  </BaseDialog>
</template>

<style scoped lang="sass">
.specification-function-form
  display: grid
  gap: 20px

  &__field
    display: grid
    gap: 8px

  &__label
    font-size: 14px
    font-weight: 600
    line-height: 20px

  &__control
    width: 100%
    min-height: 120px
    padding: 12px 14px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-surface)
    color: var(--color-text)
    font: inherit
    line-height: 22px
    resize: vertical

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

    &[aria-invalid="true"]
      border-color: var(--color-danger)

  &__error,
  &__request-error
    margin: 0
    color: var(--color-danger)
    font-size: 14px
    line-height: 20px

  &__actions
    display: flex
    justify-content: flex-end
    gap: 12px

@media (max-width: 480px)
  .specification-function-form__actions
    flex-direction: column-reverse

    :deep(.base-button)
      width: 100%
</style>
