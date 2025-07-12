# Micro-Frontend Architecture with Azure AD Authentication Implementation Guide

## Overview

This guide provides a comprehensive approach to implementing a micro-frontend architecture using React applications with shared Azure AD authentication via webpack module federation.

## Architecture Components

### Application Structure
- **Parent Application (Shell/Host)**: Main orchestrator with Azure AD authentication
- **Child Application 1**: Existing React app with Azure AD (to be integrated)
- **Child Application 2**: New React app (to be created)
- **Backend Applications**: Separate APIs for each micro-frontend

### Key Technologies
- **Webpack 5 Module Federation**: Code sharing and runtime integration
- **Azure AD (MSAL)**: Authentication provider
- **React 18**: Frontend framework
- **GitLab**: Separate repositories for each application

## Implementation Strategy

### 1. Parent Application Setup

#### Azure AD Configuration
```javascript
// parent-app/src/auth/auth-config.js
import { Configuration } from '@azure/msal-browser';

export const msalConfig = {
  auth: {
    clientId: process.env.REACT_APP_AZURE_CLIENT_ID,
    authority: process.env.REACT_APP_AZURE_AUTHORITY,
    redirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'localStorage',
  },
  system: {
    allowNativeBroker: false,
  }
};

export const loginRequest = {
  scopes: ['openid', 'profile', 'email'],
};
```

#### Authentication Provider Setup
```javascript
// parent-app/src/providers/AuthProvider.jsx
import React from 'react';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig } from '../auth/auth-config';

const msalInstance = new PublicClientApplication(msalConfig);

export const AuthProvider = ({ children }) => {
  return (
    <MsalProvider instance={msalInstance}>
      {children}
    </MsalProvider>
  );
};
```

#### Module Federation Configuration
```javascript
// parent-app/webpack.config.js
const ModuleFederationPlugin = require('@module-federation/webpack');

module.exports = {
  plugins: [
    new ModuleFederationPlugin({
      name: 'parentApp',
      filename: 'remoteEntry.js',
      remotes: {
        childApp1: 'childApp1@http://localhost:3001/remoteEntry.js',
        childApp2: 'childApp2@http://localhost:3002/remoteEntry.js',
      },
      shared: {
        react: { singleton: true, eager: true },
        'react-dom': { singleton: true, eager: true },
        '@azure/msal-browser': { singleton: true, eager: true },
        '@azure/msal-react': { singleton: true, eager: true },
      },
      exposes: {
        './AuthContext': './src/auth/AuthContext',
        './AuthProvider': './src/providers/AuthProvider',
      },
    }),
  ],
};
```

#### Main Application Component
```javascript
// parent-app/src/App.jsx
import React, { Suspense } from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './providers/AuthProvider';
import { MsalAuthenticationTemplate } from '@azure/msal-react';
import { loginRequest } from './auth/auth-config';

// Lazy load child applications
const ChildApp1 = React.lazy(() => import('childApp1/App'));
const ChildApp2 = React.lazy(() => import('childApp2/App'));

function App() {
  return (
    <AuthProvider>
      <MsalAuthenticationTemplate
        interactionType="redirect"
        authenticationRequest={loginRequest}
      >
        <Router>
          <div className="app">
            <nav>
              {/* Navigation menu for switching between child apps */}
              <a href="/child1">Child App 1</a>
              <a href="/child2">Child App 2</a>
            </nav>
            
            <main>
              <Suspense fallback={<div>Loading...</div>}>
                <Routes>
                  <Route 
                    path="/child1/*" 
                    element={<ChildApp1 />} 
                  />
                  <Route 
                    path="/child2/*" 
                    element={<ChildApp2 />} 
                  />
                  <Route 
                    path="/" 
                    element={<div>Welcome to Parent App</div>} 
                  />
                </Routes>
              </Suspense>
            </main>
          </div>
        </Router>
      </MsalAuthenticationTemplate>
    </AuthProvider>
  );
}

export default App;
```

### 2. Child Application Setup

#### Module Federation Configuration for Child Apps
```javascript
// child-app-1/webpack.config.js
const ModuleFederationPlugin = require('@module-federation/webpack');

module.exports = {
  plugins: [
    new ModuleFederationPlugin({
      name: 'childApp1',
      filename: 'remoteEntry.js',
      exposes: {
        './App': './src/App',
        './routes': './src/routes',
      },
      remotes: {
        parentApp: 'parentApp@http://localhost:3000/remoteEntry.js',
      },
      shared: {
        react: { singleton: true },
        'react-dom': { singleton: true },
        '@azure/msal-browser': { singleton: true },
        '@azure/msal-react': { singleton: true },
      },
    }),
  ],
};
```

#### Child App Component with Shared Authentication
```javascript
// child-app-1/src/App.jsx
import React from 'react';
import { Routes, Route } from 'react-router-dom';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import Dashboard from './components/Dashboard';
import Profile from './components/Profile';

function ChildApp1() {
  const { accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  if (!isAuthenticated) {
    return <div>Please authenticate through the parent application</div>;
  }

  return (
    <div className="child-app-1">
      <h1>Child Application 1</h1>
      <p>Welcome, {accounts[0]?.name}</p>
      
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/profile" element={<Profile />} />
      </Routes>
    </div>
  );
}

export default ChildApp1;
```

### 3. Authentication Context Sharing

#### Shared Authentication Hook
```javascript
// shared/src/hooks/useSharedAuth.js
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { useCallback } from 'react';

export const useSharedAuth = () => {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  const logout = useCallback(() => {
    instance.logoutRedirect();
  }, [instance]);

  const getAccessToken = useCallback(async (scopes) => {
    try {
      const response = await instance.acquireTokenSilent({
        scopes,
        account: accounts[0],
      });
      return response.accessToken;
    } catch (error) {
      // Handle token acquisition error
      console.error('Token acquisition failed:', error);
      throw error;
    }
  }, [instance, accounts]);

  return {
    isAuthenticated,
    user: accounts[0],
    logout,
    getAccessToken,
  };
};
```

### 4. Communication Between Micro-frontends

#### Event Bus for Cross-App Communication
```javascript
// shared/src/utils/eventBus.js
import { Subject } from 'rxjs';

class EventBus {
  constructor() {
    this.subject = new Subject();
  }

  emit(event, data) {
    this.subject.next({ event, data });
  }

  on(event, callback) {
    return this.subject.subscribe(({ event: eventName, data }) => {
      if (eventName === event) {
        callback(data);
      }
    });
  }
}

export const eventBus = new EventBus();
```

### 5. Deployment Strategy

#### Separate CI/CD Pipelines

**Parent App GitLab CI/CD:**
```yaml
# parent-app/.gitlab-ci.yml
stages:
  - build
  - test
  - deploy

build:
  stage: build
  script:
    - npm install
    - npm run build
  artifacts:
    paths:
      - dist/

deploy:
  stage: deploy
  script:
    - # Deploy to your hosting platform
    - # Update environment variables for child app URLs
```

**Child App GitLab CI/CD:**
```yaml
# child-app-1/.gitlab-ci.yml
stages:
  - build
  - test
  - deploy

build:
  stage: build
  script:
    - npm install
    - npm run build
  artifacts:
    paths:
      - dist/

deploy:
  stage: deploy
  script:
    - # Deploy child app independently
    - # Notify parent app of new deployment URL if needed
```

### 6. Independent vs. Integrated Deployment

#### Running Child Apps Independently

Child applications can run independently by including their own authentication setup:

```javascript
// child-app-1/src/standalone.jsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import { AuthProvider } from './auth/StandaloneAuthProvider';
import App from './App';

// Only render independently if not loaded as a micro-frontend
if (!window.__FEDERATION_RUNTIME__) {
  const root = ReactDOM.createRoot(document.getElementById('root'));
  root.render(
    <AuthProvider>
      <App />
    </AuthProvider>
  );
}
```

### 7. Best Practices and Considerations

#### TypeScript Support
```typescript
// Add type declarations for remote modules
// child-app-1/src/types/federation.d.ts
declare module 'parentApp/AuthContext' {
  export const AuthContext: React.Context<any>;
}

declare module 'childApp2/SomeComponent' {
  const component: React.ComponentType<any>;
  export default component;
}
```

#### Error Handling
```javascript
// Error boundary for micro-frontend loading
import React from 'react';

class MicrofrontendErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    console.error('Microfrontend loading error:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return <div>Something went wrong loading this section.</div>;
    }

    return this.props.children;
  }
}
```

#### Performance Optimization
```javascript
// Preload critical micro-frontends
const preloadMicrofrontend = (remoteName) => {
  const script = document.createElement('script');
  script.src = `${MICROFRONTEND_URLS[remoteName]}/remoteEntry.js`;
  script.onload = () => {
    console.log(`${remoteName} preloaded`);
  };
  document.head.appendChild(script);
};

// Preload on parent app initialization
useEffect(() => {
  preloadMicrofrontend('childApp1');
  preloadMicrofrontend('childApp2');
}, []);
```

## Answers to Your Specific Questions

### Can child apps run separately?
**Yes**, child applications can run independently by:
1. Having their own authentication setup for standalone mode
2. Detecting if they're loaded as micro-frontends
3. Rendering independently when not federated
4. Including fallback authentication providers

### Authentication Implementation
- **Centralized**: Azure AD authentication is handled in the parent app
- **Shared**: Authentication context is shared via module federation
- **Consistent**: All child apps receive the same authentication state
- **Secure**: Tokens are managed centrally and shared securely

### Repository Structure
- Each application maintains its own GitLab repository
- Independent CI/CD pipelines for each app
- Shared libraries can be published as npm packages
- Environment-specific configuration for micro-frontend URLs

## Conclusion

This architecture provides:
- **Scalability**: Teams can work independently on different applications
- **Flexibility**: Child apps can run standalone or as micro-frontends
- **Security**: Centralized Azure AD authentication with secure token sharing
- **Maintainability**: Independent deployment and versioning
- **Performance**: Runtime code sharing and lazy loading capabilities

The key to success is proper module federation configuration, shared authentication context, and robust error handling for a seamless user experience across all micro-frontends.