<script setup>
import { computed, ref, useId } from 'vue'
import StatusMessage from './StatusMessage.vue'

const props = defineProps({
  accept: { type: Array, default: () => ['audio/*', 'video/*'] },
  maxBytes: { type: Number, default: 100 * 1024 * 1024 },
})
const emit = defineEmits(['select', 'reject'])
const inputId = useId()
const error = ref('')
const dragging = ref(false)
const limit = computed(() => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(props.maxBytes / 1024 / 1024))

function selectFiles(files) {
  dragging.value = false
  if (!files?.length) return
  const file = files[0]
  const accepted = props.accept.some((rule) => rule.endsWith('/*')
    ? file.type.startsWith(rule.slice(0, -1))
    : file.type === rule)

  if (files.length > 1) error.value = 'Выберите один файл записи встречи.'
  else if (!accepted) error.value = 'Выберите аудио или видео в поддерживаемом формате.'
  else if (file.size > props.maxBytes) error.value = `Файл слишком большой. Максимальный размер — ${limit.value} МБ.`
  else {
    error.value = ''
    emit('select', file)
    return
  }
  emit('reject', error.value)
}

function onChange(event) {
  selectFiles(event.target.files)
  event.target.value = ''
}
</script>

<template>
  <div
    class="file-dropzone"
    :class="{ 'file-dropzone--dragging': dragging }"
    @dragover.prevent="dragging = true"
    @dragleave.prevent="dragging = false"
    @drop.prevent="selectFiles($event.dataTransfer.files)"
  >
    <label class="file-dropzone__area" :for="inputId">
      <span class="file-dropzone__title">Перетащите запись сюда</span>
      <span class="file-dropzone__action">или выберите файл</span>
      <span :id="`${inputId}-hint`" class="file-dropzone__hint">Аудио или видео, до {{ limit }} МБ</span>
      <input
        :id="inputId"
        class="file-dropzone__input"
        type="file"
        :accept="accept.join(',')"
        :aria-describedby="`${inputId}-hint`"
        :aria-invalid="error ? 'true' : undefined"
        @change="onChange"
      >
    </label>
    <StatusMessage v-if="error" state="error">{{ error }}</StatusMessage>
  </div>
</template>

<style scoped lang="sass">
.file-dropzone
  display: grid
  gap: 12px
  min-width: 0

  &__area
    position: relative
    display: flex
    flex-direction: column
    align-items: center
    gap: 8px
    padding: 32px 16px
    border: 1px dashed var(--color-muted)
    border-radius: 10px
    background: var(--color-background)
    text-align: center
    cursor: pointer

    &:focus-within
      outline: 3px solid var(--color-focus)
      outline-offset: 3px

  &--dragging &__area
    border-color: var(--color-accent-strong)
    background: var(--color-accent-soft)

  &__title
    font-weight: 600
    line-height: 1.5

  &__action
    color: var(--color-accent-strong)
    text-decoration: underline
    text-underline-offset: 3px

  &__hint
    color: var(--color-muted)
    font-size: 13px
    line-height: 1.5

  &__input
    position: absolute
    width: 1px
    height: 1px
    overflow: hidden
    clip-path: inset(50%)
    white-space: nowrap
</style>
