import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, it } from 'vitest'
import appRouter from '@/app/router'
import LandingPage from './LandingPage.vue'
import landingSource from './LandingPage.vue?raw'

it.each([
  ['Регистрация', 'sign-up'],
  ['Зарегистрироваться бесплатно', 'sign-up'],
  ['Попробовать бесплатно', 'sign-up'],
  ['Войти', 'sign-in'],
  ['Войти →', 'sign-in'],
])('navigates %s to the named %s route', async (label, destination) => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'landing', component: LandingPage },
      { path: '/register-test', name: 'sign-up', component: { template: '<p>Registration</p>' } },
      { path: '/login-test', name: 'sign-in', component: { template: '<p>Login</p>' } },
    ],
  })
  await router.push('/')
  await router.isReady()
  const wrapper = mount(LandingPage, { global: { plugins: [router] } })
  const link = wrapper.findAll('a').find((item) => item.text() === label)
  expect(link, `Missing CTA: ${label}`).toBeDefined()
  await link.trigger('click')
  await flushPromises()
  expect(router.currentRoute.value.name).toBe(destination)
  wrapper.unmount()
})

it('registers the landing page on the application entry route', async () => {
  await appRouter.push('/')
  await appRouter.isReady()
  expect(appRouter.currentRoute.value.matched[0].components?.default).toBe(LandingPage)
})

it('keeps the audited landing visual geometry', async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'landing', component: LandingPage },
      { path: '/sign-in', name: 'sign-in', component: { template: '<p />' } },
      { path: '/sign-up', name: 'sign-up', component: { template: '<p />' } },
    ],
  })
  await router.push('/')
  await router.isReady()
  const wrapper = mount(LandingPage, { global: { plugins: [router] } })

  const photo = wrapper.find('.landing-page__photo').element
  const badge = wrapper.find('.landing-page__badge').element
  const lead = wrapper.find('.landing-page__lead').element
  const card = wrapper.find('.landing-page__capability').element
  const primaryAction = wrapper.find('.landing-page__primary-action').element
  expect(landingSource).toMatch(/&__photo[\s\S]*?left: -7\.27%[\s\S]*?width: 114\.55%/)
  expect(landingSource).toMatch(/&__lead[\s\S]*?opacity: \.78/)
  expect(landingSource).toMatch(/&__primary-action[\s\S]*?padding: 16px 36px/)
  expect(landingSource).toContain('border: 1px solid rgba(241, 54, 29, .4)')
  expect(landingSource).toContain('border: 1px solid #e5e7eb')
  expect(landingSource).toContain('box-shadow: 0 1px 3px rgba(0, 0, 0, .04)')
  expect(landingSource).toMatch(/&__capabilities[\s\S]*?grid-auto-rows: 1fr/)
  expect(landingSource).toMatch(/&__capability[\s\S]*?height: 100%/)
  expect(photo).toBeDefined()
  expect(badge).toBeDefined()
  expect(lead).toBeDefined()
  expect(card).toBeDefined()
  expect(primaryAction).toBeDefined()
  wrapper.unmount()
})
