import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import { configureApi } from './shared/api/client'
import { clearSession, getSession, refreshSession } from './entities/session'
import router from './router'
import './app/styles/global.sass'

configureApi({
  getAccessToken: () => getSession()?.accessToken,
  refreshAccessToken: async () => {
    try {
      await refreshSession()
      return true
    } catch {
      return false
    }
  },
  onUnauthorized: clearSession,
})

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')
