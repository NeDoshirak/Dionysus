<script setup>
import { BaseButton } from '@/shared/ui'
import meeting from './assets/meeting.png'
import recordingIcon from './assets/recording.svg'
import analysisIcon from './assets/analysis.svg'
import specificationIcon from './assets/specification.svg'
import sourceIcon from './assets/source.svg'
import editIcon from './assets/edit.svg'
import exportIcon from './assets/export.svg'
import uploadIcon from './assets/upload.svg'
import arrowIcon from './assets/arrow.svg'
import processIcon from './assets/process.svg'
import documentIcon from './assets/document.svg'

const capabilities = [
  { title: 'Загрузка записи', description: 'Аудио или видео встречи в популярных форматах: MP3, MP4, WAV, M4A и других.', icon: recordingIcon },
  { title: 'Анализ разговора', description: 'ИИ выделяет требования, роли пользователей, условия, договорённости и открытые вопросы.', icon: analysisIcon },
  { title: 'Структурированное ТЗ', description: 'Готовый документ: функция, пользователь, требование, статус — в удобной таблице.', icon: specificationIcon },
  { title: 'Связь с разговором', description: 'Каждое требование привязано к фрагменту транскрипции. Нажмите — увидите источник.', icon: sourceIcon },
  { title: 'Редактирование', description: 'Правьте, добавляйте, удаляйте требования. Ставьте метку «уточнить» на спорные пункты.', icon: editIcon },
  { title: 'Экспорт', description: 'Выгружайте готовое ТЗ в форматах DOCX, PDF или JSON для дальнейшей работы.', icon: exportIcon },
]

const steps = [
  { title: 'Загрузите запись', description: 'Прикрепите аудио или видео встречи с заказчиком — до 100 МБ.', icon: uploadIcon },
  { title: 'ИИ анализирует', description: 'Система распознаёт речь, выделяет смысл и формирует структурированные требования.', icon: processIcon },
  { title: 'Получите ТЗ', description: 'Редактируйте, проверяйте связь с источником и экспортируйте в нужном формате.', icon: documentIcon },
]
</script>

<template>
  <div class="landing-page">
    <a class="landing-page__skip" href="#main-content">К основному содержимому</a>
    <header class="landing-page__header">
      <div class="landing-page__container landing-page__navigation">
        <RouterLink class="landing-page__brand" :to="{ name: 'landing' }" aria-label="SpecScribe — главная">SpecScribe</RouterLink>
        <nav class="landing-page__account" aria-label="Аккаунт">
          <BaseButton :to="{ name: 'sign-in' }" variant="outline">Войти</BaseButton>
          <BaseButton :to="{ name: 'sign-up' }">Регистрация</BaseButton>
        </nav>
      </div>
    </header>

    <main id="main-content" tabindex="-1">
      <section class="landing-page__hero" aria-labelledby="hero-title">
        <img class="landing-page__photo" :src="meeting" alt="" fetchpriority="high" width="1094" height="616">
        <div class="landing-page__hero-shade" />
        <div class="landing-page__intro">
          <p class="landing-page__badge">ИИ-инструмент для аналитиков</p>
          <h1 id="hero-title" class="landing-page__headline">Из разговора — в<br>готовое ТЗ за 5 минут</h1>
          <p class="landing-page__lead">Загрузите запись встречи и получите структурированное техническое задание с автоматическим разбором требований, ролей и открытых вопросов.</p>
          <div class="landing-page__hero-actions">
            <BaseButton class="landing-page__primary-action" :to="{ name: 'sign-up' }">Зарегистрироваться бесплатно</BaseButton>
            <BaseButton :to="{ name: 'sign-in' }" variant="text">Войти →</BaseButton>
          </div>
        </div>
      </section>

      <section class="landing-page__section landing-page__section--capabilities" aria-labelledby="capabilities-title">
        <div class="landing-page__container">
          <p class="landing-page__eyebrow">Возможности</p>
          <h2 id="capabilities-title" class="landing-page__section-title">Что умеет SpecScribe</h2>
          <ul class="landing-page__capabilities">
            <li v-for="capability in capabilities" :key="capability.title" class="landing-page__capability">
              <span class="landing-page__icon"><img :src="capability.icon" alt="" width="28" height="28"></span>
              <h3 class="landing-page__item-title">{{ capability.title }}</h3>
              <p class="landing-page__description">{{ capability.description }}</p>
            </li>
          </ul>
        </div>
      </section>

      <section class="landing-page__section" aria-labelledby="process-title">
        <div class="landing-page__container">
          <p class="landing-page__eyebrow">Процесс</p>
          <h2 id="process-title" class="landing-page__section-title">Как это работает</h2>
          <ol class="landing-page__steps">
            <li v-for="(step, index) in steps" :key="step.title" class="landing-page__step">
              <span class="landing-page__icon landing-page__icon--step"><img :src="step.icon" alt="" width="32" height="32"></span>
              <h3 class="landing-page__item-title">{{ step.title }}</h3>
              <p class="landing-page__description">{{ step.description }}</p>
              <img v-if="index < steps.length - 1" class="landing-page__step-arrow" :src="arrowIcon" alt="" width="24" height="24">
            </li>
          </ol>
          <div class="landing-page__try">
            <BaseButton class="landing-page__primary-action" :to="{ name: 'sign-up' }">Попробовать бесплатно</BaseButton>
          </div>
        </div>
      </section>
    </main>

    <footer class="landing-page__footer">
      <div class="landing-page__container landing-page__footer-content">
        <RouterLink class="landing-page__brand landing-page__brand--footer" :to="{ name: 'landing' }">SpecScribe</RouterLink>
        <div class="landing-page__legal">
          <span>Политика конфиденциальности</span>
          <span>Условия использования</span>
        </div>
        <p class="landing-page__copyright">© 2026 SpecScribe</p>
      </div>
    </footer>
  </div>
</template>

<style scoped lang="sass">
.landing-page
  &__container
    width: min(100%, var(--content-width))
    margin-inline: auto
    padding-inline: 24px

  &__skip
    position: absolute
    z-index: 3
    top: 8px
    left: 8px
    padding: 12px
    transform: translateY(-160%)
    background: var(--color-surface)
    color: var(--color-text)

    &:focus
      transform: none

  &__header
    position: absolute
    z-index: 2
    top: 0
    left: 0
    width: 100%
    border-bottom: 1px solid var(--color-border)
    background: rgb(249 250 251 / 85%)
    backdrop-filter: blur(10px)

  &__navigation
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px
    min-height: 64px

  &__brand
    color: var(--color-accent-strong)
    font-size: 22px
    font-weight: 700
    line-height: 1.5
    letter-spacing: -.5px

    &--footer
      color: var(--color-accent)
      font-size: 18px

  &__account
    display: flex
    align-items: center
    gap: 12px

  &__hero
    position: relative
    isolation: isolate
    display: flex
    align-items: flex-start
    justify-content: center
    min-height: 586px
    padding: 120px 24px 80px
    overflow: hidden
    background: #484647
    color: var(--color-surface)

  &__photo
    position: absolute
    z-index: -2
    inset: -2.5%
    width: 105%
    height: 105%
    object-fit: cover
    filter: blur(3px)

  &__hero-shade
    position: absolute
    z-index: -1
    inset: 0
    background: linear-gradient(180deg, rgb(10 5 4 / 55%) 0%, rgb(10 5 4 / 45%) 60%, rgb(38 34 33 / 38%) 70%, rgb(78 75 75 / 31%) 80%, rgb(140 139 139 / 25%) 90%, rgb(249 250 251 / 18%) 100%)

  &__intro
    width: min(100%, 632px)
    text-align: center

  &__badge
    display: inline-block
    margin: 0 0 20px
    padding: 4px 12px
    border: 1px solid rgb(241 54 29 / 40%)
    border-radius: 100px
    background: rgb(241 54 29 / 25%)
    color: #ffbcb4
    font-size: 13px
    font-weight: 600
    line-height: 1.5
    letter-spacing: 1px
    text-transform: uppercase

  &__headline
    margin: 0
    font-size: 52px
    font-weight: 700
    line-height: 1.1
    letter-spacing: -1.5px

  &__lead
    max-width: 540px
    margin: 20px auto 0
    color: rgb(255 255 255 / 90%)
    font-size: 18px
    line-height: 1.7

  &__hero-actions
    display: flex
    align-items: center
    justify-content: center
    gap: 16px
    margin-top: 36px

  &__primary-action
    padding: 15px 35px
    font-size: 18px

  &__section
    padding-block: 88px
    background: var(--color-background)

    &--capabilities
      background: var(--color-surface)

  &__eyebrow
    margin: 0 0 12px
    color: var(--color-accent-strong)
    font-size: 13px
    font-weight: 600
    line-height: 1.5
    letter-spacing: 1px
    text-transform: uppercase

  &__section-title
    margin: 0
    font-size: 32px
    font-weight: 600
    line-height: 1.5
    letter-spacing: -.5px

  &__capabilities
    display: grid
    grid-template-columns: repeat(2, minmax(0, 1fr))
    gap: 20px
    margin: 48px 0 0
    padding: 0
    list-style: none

  &__capability
    padding: 28px 24px
    border: 1px solid var(--color-border)
    border-radius: 12px
    box-shadow: 0 1px 3px rgb(0 0 0 / 4%)

  &__icon
    display: flex
    align-items: center
    justify-content: center
    width: 48px
    height: 48px
    border-radius: 10px
    background: var(--color-accent-soft)

    &--step
      width: 52px
      height: 52px
      margin-inline: auto
      border-radius: 12px

  &__item-title
    margin: 16px 0 8px
    font-size: 17px
    font-weight: 600
    line-height: 1.5

  &__description
    margin: 0
    color: var(--color-muted)
    font-size: 15px
    line-height: 1.6

  &__steps
    display: grid
    grid-template-columns: repeat(3, minmax(0, 1fr))
    gap: 24px
    margin: 48px 0 0
    padding: 0
    list-style: none

  &__step
    position: relative
    padding-inline: 12px
    text-align: center

  &__step-arrow
    position: absolute
    top: 10px
    right: -24px

  &__try
    margin-top: 52px
    text-align: center

  &__footer
    padding-block: 40px
    background: var(--color-text)

  &__footer-content
    display: flex
    align-items: center
    justify-content: space-between
    gap: 24px

  &__legal
    display: flex
    flex-wrap: wrap
    gap: 12px 24px
    color: #9ca3af
    font-size: 14px
    line-height: 1.5

  &__copyright
    flex-shrink: 0
    margin: 0
    color: #9ca3af
    font-size: 14px
    line-height: 1.5

@media (max-width: 640px)
  .landing-page
    &__container
      padding-inline: 20px

    &__navigation
      flex-wrap: wrap
      gap: 8px 12px
      padding-block: 10px

    &__brand
      font-size: 20px

    &__account
      gap: 8px

    &__hero
      min-height: 620px
      padding: 152px 20px 64px

    &__headline
      font-size: clamp(34px, 8vw, 44px)
      letter-spacing: -1px

    &__badge
      font-size: 11px
      letter-spacing: .5px

    &__lead
      font-size: 16px

    &__hero-actions
      flex-direction: column
      gap: 8px
      margin-top: 28px

    &__primary-action
      max-width: 100%
      padding-inline: 22px
      font-size: 16px

    &__section
      padding-block: 56px

    &__section-title
      font-size: 28px
      line-height: 1.3

    &__capabilities
      grid-template-columns: minmax(0, 1fr)
      margin-top: 32px
      gap: 16px

    &__steps
      grid-template-columns: minmax(0, 1fr)
      gap: 48px
      margin-top: 32px

    &__step
      max-width: 360px
      margin-inline: auto

    &__step-arrow
      top: auto
      right: calc(50% - 12px)
      bottom: -36px
      transform: rotate(90deg)

    &__footer-content
      align-items: flex-start
      flex-direction: column

    &__legal
      flex-direction: column
</style>
