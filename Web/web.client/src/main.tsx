import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import AppHeader from './components/AppHeader'
import AppFooter from './components/AppFooter'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <div className="app-layout">
      <AppHeader />
      <main className="app-main">
        <App />
      </main>
      <AppFooter />
    </div>
  </StrictMode>,
)
