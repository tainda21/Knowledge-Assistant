import { useAuth } from '../contexts/AuthContext'
import ConversationList from './ConversationList'
import type { ConversationSummary } from './ConversationList'
import styles from './Sidebar.module.css'

interface Props {
  conversations: ConversationSummary[]
  activeConversationId: string | null
  view: 'chat' | 'admin'
  isOpen: boolean
  isSending: boolean
  onSelectConversation: (id: string) => void
  onDeleteConversation: (id: string) => void
  onNewChat: () => void
  onSetView: (view: 'chat' | 'admin') => void
}

export default function Sidebar({
  conversations,
  activeConversationId,
  view,
  isOpen,
  isSending,
  onSelectConversation,
  onDeleteConversation,
  onNewChat,
  onSetView,
}: Props) {
  const { user, logout } = useAuth()

  const initials = user
    ? user.username.slice(0, 2).toUpperCase()
    : '??'

  return (
    <nav
      className={`${styles.sidebar} ${isOpen ? styles.open : ''}`}
      aria-label="Application sidebar"
    >
      {/* Logo / App name */}
      <div className={styles.header}>
        <div className={styles.logoIcon} aria-hidden="true">✦</div>
        <span className={styles.appName}>Knowledge Assistant</span>
      </div>

      {/* New Chat */}
      <button
        className={styles.newChat}
        onClick={() => { onNewChat(); onSetView('chat') }}
        disabled={isSending}
        title="Start a new conversation"
      >
        <span className={styles.newChatIcon}>＋</span>
        New Chat
      </button>

      {/* Conversation list */}
      <p className={styles.sectionLabel}>Conversations</p>
      <ConversationList
        conversations={conversations}
        activeId={activeConversationId}
        onSelect={id => { onSelectConversation(id); onSetView('chat') }}
        onDelete={onDeleteConversation}
        disabled={isSending}
      />

      {/* Admin panel link */}
      {user?.role === 'Admin' && (
        <button
          className={`${styles.adminBtn} ${view === 'admin' ? styles.active : ''}`}
          onClick={() => onSetView(view === 'admin' ? 'chat' : 'admin')}
        >
          ⚙ Admin panel
        </button>
      )}

      {/* Footer */}
      <div className={styles.footer}>
        <div className={styles.userInfo}>
          <div className={styles.avatar} aria-hidden="true">{initials}</div>
          <div className={styles.userDetails}>
            <span className={styles.userName}>{user?.username ?? '—'}</span>
            <span className={styles.userRole}>{user?.role ?? ''}</span>
          </div>
          <button
            className={styles.logoutBtn}
            onClick={logout}
            title="Sign out"
            aria-label="Sign out"
          >
            ↩
          </button>
        </div>
      </div>
    </nav>
  )
}
