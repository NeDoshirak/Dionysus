<script setup>
import { computed, ref, watch } from 'vue'
import { editableItemKinds, itemKindLabels } from '@/entities/specification'
import { BaseButton, BaseDialog, BaseInput } from '@/shared/ui'

const requirementPriorities = [
  { value: 'required', label: 'Required' },
  { value: 'desirable', label: 'Desirable' },
  { value: 'future', label: 'Future' },
  { value: 'unknown', label: 'Unknown' },
]

const questionReasons = [
  { value: 'contradiction', label: 'Contradiction' },
  { value: 'unresolved', label: 'Unresolved' },
  { value: 'missingInformation', label: 'MissingInformation' },
]

const props = defineProps({
  open: Boolean,
  mode: { type: String, default: 'create' },
  item: { type: Object, default: null },
  error: { type: String, default: '' },
})
const emit = defineEmits(['save', 'cancel'])

const kind = ref(editableItemKinds[0])
const title = ref('')
const description = ref('')
const priority = ref('required')
const descriptionError = ref('')

const isEdit = computed(() => props.mode === 'edit' && props.item)
const currentKind = computed(() => isEdit.value ? props.item.kind : kind.value)
const priorityOptions = computed(() => {
  if (currentKind.value === 'functionalRequirement') return requirementPriorities
  if (currentKind.value === 'keyQuestion') return questionReasons
  return []
})

watch(() => [props.open, props.item, props.mode], () => {
  kind.value = props.item?.kind || editableItemKinds[0]
  title.value = props.item?.title || ''
  description.value = props.item?.description || ''
  priority.value = props.item?.priority || (kind.value === 'keyQuestion' ? 'contradiction' : 'required')
  descriptionError.value = ''
}, { immediate: true })

watch(currentKind, (nextKind) => {
  if (nextKind === 'functionalRequirement' && !requirementPriorities.some((option) => option.value === priority.value)) {
    priority.value = 'required'
  } else if (nextKind === 'keyQuestion' && !questionReasons.some((option) => option.value === priority.value)) {
    priority.value = 'contradiction'
  }
})

function submit() {
  descriptionError.value = description.value.trim() ? '' : 'Добавьте описание'
  if (descriptionError.value) return

  const body = {
    title: title.value.trim(),
    description: description.value.trim(),
    sourceStatementIds: isEdit.value && props.item?.sourceStatementIds ? [...props.item.sourceStatementIds] : [],
  }

  if (!isEdit.value) body.kind = kind.value
  if (priorityOptions.value.length) body.priority = priority.value

  emit('save', body)
}
</script>

<template>
  <BaseDialog
    :open="open"
    :title="isEdit ? 'Редактировать карточку' : 'Новая карточка'"
    @close="emit('cancel')"
  >
    <form class="specification-card-form" @submit.prevent="submit">
      <label v-if="!isEdit" class="specification-card-form__field">
        <span class="specification-card-form__label">Тип карточки</span>
        <select v-model="kind" class="specification-card-form__control" name="kind">
          <option v-for="itemKind in editableItemKinds" :key="itemKind" :value="itemKind">
            {{ itemKindLabels[itemKind] }}
          </option>
        </select>
      </label>

      <BaseInput
        v-model="title"
        name="title"
        label="Заголовок"
        placeholder="Например, SSO"
      />

      <label class="specification-card-form__field">
        <span class="specification-card-form__label">Описание</span>
        <textarea
          v-model="description"
          class="specification-card-form__control specification-card-form__control--textarea"
          name="description"
          placeholder="Опишите требование"
          :aria-invalid="descriptionError ? 'true' : undefined"
          @input="descriptionError = ''"
        />
        <span v-if="descriptionError" class="specification-card-form__error" role="alert">{{ descriptionError }}</span>
      </label>

      <label v-if="priorityOptions.length" class="specification-card-form__field">
        <span class="specification-card-form__label">{{ currentKind === 'keyQuestion' ? 'Причина' : 'Приоритет' }}</span>
        <select v-model="priority" class="specification-card-form__control" name="priority">
          <option v-for="option in priorityOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </label>

      <p v-if="error" class="specification-card-form__request-error" role="alert">{{ error }}</p>

      <div class="specification-card-form__actions">
        <BaseButton type="button" variant="outline" @click="emit('cancel')">Отмена</BaseButton>
        <BaseButton type="submit">Сохранить</BaseButton>
      </div>
    </form>
  </BaseDialog>
</template>

<style scoped lang="sass">
.specification-card-form
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
    min-height: 49px
    padding: 12px 14px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-surface)
    color: var(--color-text)
    font: inherit
    line-height: 22px

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

    &[aria-invalid="true"]
      border-color: var(--color-danger)

    &--textarea
      min-height: 120px
      resize: vertical

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
  .specification-card-form__actions
    flex-direction: column-reverse

    :deep(.base-button)
      width: 100%
</style>
