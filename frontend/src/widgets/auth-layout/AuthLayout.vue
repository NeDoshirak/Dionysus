<template>
  <main class="auth-layout">
    <aside class="auth-layout__panel" aria-label="О продукте">
      <div class="auth-layout__image-placeholder" role="img" aria-label="Декоративная иллюстрация SpecScribe">Иллюстрация</div>
      <div class="auth-layout__panel-copy">
        <p class="auth-layout__slogan">Ваши встречи — в структурированные ТЗ</p>
        <p class="auth-layout__benefits">ИИ-анализ · Автоматические требования · Экспорт</p>
      </div>
    </aside>
    <section class="auth-layout__content">
      <div class="auth-layout__card"><slot /></div>
    </section>
  </main>
</template>

<style scoped lang="sass">
.auth-layout
  display: grid
  grid-template-columns: minmax(380px, 480px) minmax(0, 1fr)
  min-height: 100vh

  &__panel
    display: flex
    flex-direction: column
    align-items: center
    justify-content: center
    padding: 60px 48px
    background: linear-gradient(127deg, #1a0a08, #2d0f08 40%, #1a0a08)
    color: var(--color-surface)

  &__image-placeholder
    display: grid
    width: 360px
    max-width: 100%
    height: 432px
    place-items: center
    border: 1px dashed rgba(255, 255, 255, .32)
    border-radius: var(--radius-lg)
    color: rgba(255, 255, 255, .48)
    font-size: 14px

  &__panel-copy
    width: min(329px, 100%)
    padding-top: 32px
    text-align: center

  &__slogan
    margin: 0
    color: rgba(255, 255, 255, .85)
    font-size: 15px
    font-weight: 600
    line-height: 22px

  &__benefits
    margin: 8px 0 0
    color: rgba(255, 255, 255, .4)
    font-size: 13px
    line-height: 21px

  &__content
    display: grid
    min-width: 0
    place-items: center
    padding: var(--space-10) var(--space-6)
    background: radial-gradient(ellipse at 60% 40%, rgba(241, 54, 29, .1), transparent 44%), var(--color-background)

  &__card
    width: min(100%, 420px)
    padding: var(--space-10)
    border-radius: var(--radius-lg)
    background: var(--color-surface)
    box-shadow: var(--shadow-card)

@media (max-width: 760px)
  .auth-layout
    display: block

    &__panel
      min-height: 205px
      padding: var(--space-7) var(--space-6)

    &__image-placeholder
      display: none

    &__panel-copy
      padding-top: 0

    &__content
      min-height: calc(100vh - 205px)
      padding: var(--space-6) var(--space-4)

    &__card
      padding: var(--space-7) var(--space-5)
</style>
