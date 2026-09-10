import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, it } from 'vitest'
import appRouter from '@/app/router'
import LandingPage from './LandingPage.vue'

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
