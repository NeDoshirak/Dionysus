<script setup>
import { nextTick, ref, watch } from 'vue'

const props = defineProps({
  modelValue: { type: String, default: '' },
  length: { type: Number, default: 6 },
  label: { type: String, default: 'Код подтверждения' },
})
const emit = defineEmits(['update:modelValue'])
const inputRefs = ref([])
const digits = ref([])

function normalize(value) {
  return String(value).replace(/\D/g, '').slice(0, props.length)
}

function updateDigits(value) {
  const normalized = normalize(value)
  digits.value = Array.from({ length: props.length }, (_, index) => normalized[index] || '')
}

function emitValue() {
  emit('update:modelValue', digits.value.join(''))
}

function setDigit(index, event) {
  digits.value[index] = normalize(event.target.value).slice(-1)
  emitValue()
  if (digits.value[index] && index < props.length - 1) inputRefs.value[index + 1]?.focus()
}

function setInputRef(element, index) {
  if (element) inputRefs.value[index] = element
}

function handleKeydown(index, event) {
  if (event.key === 'Backspace' && !digits.value[index] && index > 0) {
    event.preventDefault()
    inputRefs.value[index - 1]?.focus()
  }
}

async function handlePaste(index, event) {
  const pastedDigits = normalize(event.clipboardData?.getData('text')).slice(0, props.length - index)
  if (!pastedDigits) return

  event.preventDefault()
  for (const [offset, digit] of [...pastedDigits].entries()) digits.value[index + offset] = digit
  emitValue()
  await nextTick()
  inputRefs.value[index + pastedDigits.length - 1]?.focus()
}

watch(() => [props.modelValue, props.length], () => updateDigits(props.modelValue), { immediate: true })
</script>

<template>
  <div class="code-input" role="group" :aria-label="label">
    <input
      v-for="(_, index) in digits"
      :key="index"
      :ref="(element) => setInputRef(element, index)"
      :value="digits[index]"
      class="code-input__cell"
      inputmode="numeric"
      maxlength="1"
      :aria-label="`${label}: цифра ${index + 1}`"
      @input="setDigit(index, $event)"
      @keydown="handleKeydown(index, $event)"
      @paste="handlePaste(index, $event)"
    >
  </div>
</template>

<style scoped lang="sass">
.code-input
  display: grid
  grid-template-columns: repeat(6, 52px)
  gap: var(--space-2)
  justify-content: center

  &__cell
    width: 52px
    height: 60px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-md)
    background: var(--color-surface)
    color: var(--color-text)
    font-size: 24px
    font-weight: 600
    text-align: center

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px
</style>
