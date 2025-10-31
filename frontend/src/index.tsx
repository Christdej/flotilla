import App from './App'
import React from 'react'
import { PublicClientApplication } from '@azure/msal-browser'
import { MsalProvider } from '@azure/msal-react'
import { msalConfig } from 'api/AuthConfig'
import ReactDOM from 'react-dom/client'

// ========================================

const msalInstance = new PublicClientApplication(msalConfig)

// Error handling for redirect login
await msalInstance.initialize()

msalInstance
    .handleRedirectPromise()
    .then((tokenResponse) => {
        if (!tokenResponse) {
            const accounts = msalInstance.getAllAccounts()
            if (accounts.length === 0) {
                console.log('No accounts found, will redirect from AssetSelectionPage')
            }
        } else {
            console.log('User authenticated successfully')
        }
    })
    .catch((err) => {
        console.error('Authentication error:', err)
    })

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Failed to find the root element')
const root = ReactDOM.createRoot(rootElement)

root.render(
    <React.StrictMode>
        <MsalProvider instance={msalInstance}>
            <App />
        </MsalProvider>
    </React.StrictMode>
)
