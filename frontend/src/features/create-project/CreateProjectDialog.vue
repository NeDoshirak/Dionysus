<script setup>
import { ref } from 'vue'
import { createProject as defaultCreateProject } from '@/entities/project'
import { BaseButton, BaseDialog, BaseInput, FileDropzone, StatusMessage } from '@/shared/ui'

const props = defineProps({
  open: Boolean,
  createProject: { type: Function, default: defaultCreateProject },
})
const emit = defineEmits(['created', 'close'])

const name = ref('')
const media = ref(null)
const nameError = ref('')
const fileError = ref('')
const state = ref('idle')

function onFileSelect(file) {
  media.value = file
  fileError.value = ''
}

function onFileReject(message) {
  media.value = null
  fileError.value = message
}

function validate() {
  nameError.value = name.value.trim() ? '' : 'Введите название проекта'
  if (!media.value) fileError.value ||= 'Выберите аудио или видеофайл'
  return !nameError.value && !fileError.value
}

function errorMessage(error) {
  if (error?.code === 'conversion-failed' || error?.status === 422 || error?.code === '422') {
    return 'Не удалось преобразовать запись. Проверьте файл. Попробуйте ещё раз.'
  }
  if (error?.code === 'transcription-failed' || error?.status === 502 || error?.code === '502') {
    return 'Не удалось расшифровать запись. Попробуйте ещё раз.'
  }
  return 'Не удалось создать проект. Попробуйте ещё раз.'
}

async function submit() {
  state.value = 'idle'
  if (!validate()) return

  state.value = 'submitting'
  try {
    const project = await props.createProject({ name: name.value.trim(), media: media.value })
    state.value = 'success'
    emit('created', project)
  } catch (error) {
    state.value = 'error'
    fileError.value = errorMessage(error)
  }
}
</script>

<template>
  <BaseDialog :open="open" title="Новый проект" @close="emit('close')">
    <form class="create-project-dialog" @submit.prevent="submit">
      <p class="create-project-dialog__intro">Загрузите запись встречи, чтобы получить транскрипцию и структурированное ТЗ.</p>
      <BaseInput
        v-model="name"
        name="name"
        label="Название проекта"
        placeholder="Например, Customer discovery"
        :error="nameError"
        @update:model-value="nameError = ''"
      />
      <FileDropzone
        :accept="['audio/*', 'video/*']"
        :max-bytes="104857600"
        @select="onFileSelect"
        @reject="onFileReject"
      />
      <p v-if="media" class="create-project-dialog__file" data-testid="selected-file">Выбран файл: <strong>{{ media.name }}</strong></p>
      <StatusMessage v-if="fileError" state="error">{{ fileError }}</StatusMessage>
      <StatusMessage v-if="state === 'success'" state="success">Проект создан, запись обрабатывается</StatusMessage>
      <div class="create-project-dialog__actions">
        <BaseButton type="button" variant="outline" :disabled="state === 'submitting'" @click="emit('close')">Отмена</BaseButton>
        <BaseButton type="submit" :loading="state === 'submitting'">{{ state === 'submitting' ? 'Создание…' : 'Создать проект' }}</BaseButton>
      </div>
    </form>
  </BaseDialog>
</template>

<style scoped lang="sass">
.create-project-dialog
  display: grid
  gap: 20px

  &__intro
    margin: -4px 0 0
    color: var(--color-muted)
    line-height: 1.5

  &__file
    margin: -8px 0 0
    color: var(--color-muted)
    font-size: 14px

  &__actions
    display: flex
    justify-content: flex-end
    gap: 12px
    padding-top: 4px

@media (max-width: 480px)
  .create-project-dialog__actions
    flex-direction: column-reverse

    :deep(.base-button)
      width: 100%
</style>
