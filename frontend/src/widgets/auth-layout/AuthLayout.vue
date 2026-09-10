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
    width: min(360px, 100%)
    height: min(432px, 48vh)
    place-items: center
    border: 1px dashed rgba(255, 255, 255, .32)
    border-radius: 16px
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
    line-height: 1.5

  &__benefits
    margin: 8px 0 0
    color: rgba(255, 255, 255, .4)
    font-size: 13px
    line-height: 1.6

  &__content
    display: grid
    min-width: 0
    place-items: center
    padding: 40px 24px
    background: radial-gradient(ellipse at 60% 40%, rgba(241, 54, 29, .1), transparent 44%), var(--color-background)

  &__card
    width: min(100%, 420px)
    padding: 40px
    border-radius: 16px
    background: var(--color-surface)
    box-shadow: 0 4px 6px rgba(0, 0, 0, .08)

@media (max-width: 760px)
  .auth-layout
    display: block

    &__panel
      min-height: 205px
      padding: 28px 24px

    &__image-placeholder
      display: none

    &__panel-copy
      padding-top: 0

    &__content
      min-height: calc(100vh - 205px)
      padding: 24px 16px

    &__card
      padding: 28px 20px
</style>
