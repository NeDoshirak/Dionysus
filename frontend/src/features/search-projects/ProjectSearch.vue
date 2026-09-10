<script setup>
import { ref, watch } from 'vue'
import { findProjects } from '@/entities/project'

const props = defineProps({ search: { type: Function, default: findProjects } })
const emit = defineEmits(['results', 'state'])
const query = ref('')
const state = ref('idle')
const results = ref([])

async function runSearch(value) {
  const normalized = value.trim()
  if (normalized.length < 2) {
    state.value = normalized ? 'short' : 'idle'
    results.value = []
    emit('results', results.value)
    emit('state', state.value)
    return
  }
  state.value = 'loading'
  emit('state', state.value)
  try {
    results.value = await props.search(normalized)
    state.value = results.value.length ? 'matched' : 'empty'
  } catch {
    results.value = []
    state.value = 'error'
  }
  emit('results', results.value)
  emit('state', state.value)
}

watch(query, runSearch)
</script>

<template>
  <div class="project-search">
    <label class="project-search__label" for="project-search">Поиск проектов</label>
    <span class="project-search__icon" aria-hidden="true">⌕</span>
    <input id="project-search" v-model="query" class="project-search__input" type="search" placeholder="Поиск проектов…" />
    <p v-if="state === 'short'" class="project-search__state">Введите минимум 2 символа</p>
    <p v-else-if="state === 'loading'" class="project-search__state" aria-busy="true">Поиск…</p>
    <p v-else-if="state === 'empty'" class="project-search__state">Ничего не найдено</p>
    <p v-else-if="state === 'error'" class="project-search__state project-search__state--error" role="alert">Не удалось выполнить поиск</p>
  </div>
</template>

<style scoped lang="sass">
.project-search
  position: relative
  width: min(320px, 100%)

  &__label
    position: absolute
    width: 1px
    height: 1px
    overflow: hidden
    clip: rect(0 0 0 0)

  &__icon
    position: absolute
    top: 9px
    left: 12px
    color: var(--color-muted)
    font-size: 21px

  &__input
    width: 100%
    height: 40px
    padding: 8px 14px 8px 36px
    border: 1px solid var(--color-border)
    border-radius: 8px
    font: inherit

  &__state
    margin: 8px 0 0
    color: var(--color-muted)
    font-size: 12px

    &--error
      color: var(--color-danger)
</style>
