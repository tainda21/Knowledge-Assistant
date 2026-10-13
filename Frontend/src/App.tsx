import { useCallback, useEffect, useState } from 'react'
import { AuthProvider, useAuth, apiBaseUrl } from './contexts/AuthContext'
import LoginPage from './components/LoginPage'
import Sidebar from './components/Sidebar'
import ChatPanel from './components/ChatPanel'
import AdminPanel from './components/AdminPanel'
import type { ConversationSummary } from './components/ConversationList'
import styles from './App.module.css'

function AppShell() {
  const { user, isLoading, authFetch } = useAuth()
  const [conversations, setConversations] = useState<ConversationSummary[]>([])
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)
  const [view, setView] = useState<'chat' | 'admin'>('chat')
  const [isSending, setIsSending] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  // Fetch conversation list
  const refreshConversations = useCallback(() => {
    if (!user) return
    void authFetch(`${apiBaseUrl}/api/conversations`)
      .then(async res => {
        if (!res.ok) return
        return res.json() as Promise<ConversationSummary[]>
      })
      .then(data => { if (data) setConversations(data) })
  }, [user, authFetch])

  useEffect(() => { refreshConversations() }, [refreshConversations])

  if (isLoading) {
    return (
      <div style={{ minHeight: '100svh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--sidebar-bg)', color: 'var(--sidebar-muted)', fontSize: '0.9rem' }}>
        Loading…
      </div>
    )
  }

  if (!user) return <LoginPage />

  function handleNewChat() {
    setActiveConversationId(null)
    setView('chat')
    setSidebarOpen(false)
  }

  function handleSelectConversation(id: string) {
    setActiveConversationId(id)
    setView('chat')
    setSidebarOpen(false)
  }

  function handleDeleteConversation(id: string) {
    setConversations(prev => prev.filter(c => c.id !== id))
    if (activeConversationId === id) setActiveConversationId(null)
  }

  function handleConversationCreated(id: string) {
    setActiveConversationId(id)
    // Refresh after a short delay so the new conversation appears
    setTimeout(() => refreshConversations(), 400)
  }

  function handleIsSendingChange(v: boolean) {
    setIsSending(v)
    if (!v) refreshConversations()
  }

  return (
    <>
      {/* Mobile overlay backdrop */}
      {sidebarOpen && (
        <div
          onClick={() => setSidebarOpen(false)}
          style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', zIndex: 99 }}
          aria-hidden="true"
        />
      )}

      <div className={styles.shell}>
        <Sidebar
          conversations={conversations}
          activeConversationId={activeConversationId}
          view={view}
          isOpen={sidebarOpen}
          isSending={isSending}
          onSelectConversation={handleSelectConversation}
          onDeleteConversation={handleDeleteConversation}
          onNewChat={handleNewChat}
          onSetView={setView}
        />

        <main className={styles.main}>
          {view === 'admin' ? (
            <AdminPanel onMenuOpen={() => setSidebarOpen(true)} />
          ) : (
            <ChatPanel
              conversationId={activeConversationId}
              onMenuOpen={() => setSidebarOpen(true)}
              onConversationCreated={handleConversationCreated}
              onIsSendingChange={handleIsSendingChange}
            />
          )}
        </main>
      </div>
    </>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <AppShell />
    </AuthProvider>
  )
}
