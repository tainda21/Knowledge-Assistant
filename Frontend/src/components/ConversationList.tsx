import { useAuth, apiBaseUrl } from '../contexts/AuthContext'
import styles from './ConversationList.module.css'

export interface ConversationSummary {
  id: string
  createdAt: string
  updatedAt: string
  snippet: string | null
}

interface Props {
  conversations: ConversationSummary[]
  activeId: string | null
  onSelect: (id: string) => void
  onDelete: (id: string) => void
  disabled?: boolean
}

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const mins = Math.floor(diff / 60_000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`
  return new Date(iso).toLocaleDateString()
}

export default function ConversationList({ conversations, activeId, onSelect, onDelete, disabled }: Props) {
  const { authFetch } = useAuth()

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.stopPropagation()
    if (!confirm('Delete this conversation?')) return
    try {
      await authFetch(`${apiBaseUrl}/api/conversations/${id}`, { method: 'DELETE' })
      onDelete(id)
    } catch {
      // silently ignore
    }
  }

  if (conversations.length === 0) {
    return (
      <div className={styles.list}>
        <p className={styles.empty}>No conversations yet. Start a new chat!</p>
      </div>
    )
  }

  return (
    <div className={styles.list} role="list">
      {conversations.map(conv => (
        <button
          key={conv.id}
          role="listitem"
          className={`${styles.item} ${conv.id === activeId ? styles.active : ''}`}
          onClick={() => !disabled && onSelect(conv.id)}
          title={conv.snippet ?? 'Empty conversation'}
          aria-current={conv.id === activeId ? 'true' : undefined}
        >
          <span className={styles.snippet}>
            {conv.snippet ?? 'New conversation'}
          </span>
          <span className={styles.meta}>{relativeTime(conv.updatedAt)}</span>
          <button
            className={styles.deleteBtn}
            onClick={e => handleDelete(e, conv.id)}
            aria-label="Delete conversation"
            tabIndex={-1}
          >
            ×
          </button>
        </button>
      ))}
    </div>
  )
}
