import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import { AppShell } from '@/components/layout/AppShell'
import { LoginPage } from '@/pages/LoginPage'
import { ClientsPage } from '@/pages/ClientsPage'
import { ClientDetailPage } from '@/pages/ClientDetailPage'
import { ProjectDetailPage } from '@/pages/ProjectDetailPage'
import { EngagementDetailPage } from '@/pages/EngagementDetailPage'
import { ScanJobDetailPage } from '@/pages/ScanJobDetailPage'
import { PlaybooksPage } from '@/pages/PlaybooksPage'
import { PlaybookDetailPage } from '@/pages/PlaybookDetailPage'
import { IntegrationsPage } from '@/pages/IntegrationsPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import type { ReactNode } from 'react'

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }
  return <>{children}</>
}

function RedirectIfAuthed({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  if (isAuthenticated) {
    return <Navigate to="/clients" replace />
  }
  return <>{children}</>
}

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route
          path="/login"
          element={
            <RedirectIfAuthed>
              <LoginPage />
            </RedirectIfAuthed>
          }
        />
        <Route
          element={
            <RequireAuth>
              <AppShell />
            </RequireAuth>
          }
        >
          <Route index element={<Navigate to="/clients" replace />} />
          <Route path="clients" element={<ClientsPage />} />
          <Route path="clients/:clientId" element={<ClientDetailPage />} />
          <Route path="projects/:projectId" element={<ProjectDetailPage />} />
          <Route path="engagements/:engagementId" element={<EngagementDetailPage />} />
          <Route path="scan-jobs/:scanJobId" element={<ScanJobDetailPage />} />
          <Route path="playbooks" element={<PlaybooksPage />} />
          <Route path="playbooks/:playbookId" element={<PlaybookDetailPage />} />
          <Route path="settings/integrations" element={<IntegrationsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  )
}
