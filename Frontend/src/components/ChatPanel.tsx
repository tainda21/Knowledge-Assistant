import { useEffect, useRef, useState, useCallback } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { useAuth, apiBaseUrl } from '../contexts/AuthContext'
import styles from './ChatPanel.module.css'

type ChatMessage = { id: string; role: 'user' | 'assistant'; content: string; createdAt: string }
type ChatDocument = { name: string; content: string }

const DOCS_KEY = 'ka-documents'

function readStoredDocs(): ChatDocument[] {
  try {
    const raw = localStorage.getItem(DOCS_KEY)
    return raw ? (JSON.parse(raw) as ChatDocument[]) : []
  } catch { return [] }
}

interface Props {
  conversationId: string | null
  onMenuOpen: () => void
  onConversationCreated: (id: string) => void
  onIsSendingChange: (v: boolean) => void
}

export default function ChatPanel({ conversationId, onMenuOpen, onConversationCreated, onIsSendingChange }: Props) {
  const { authFetch } = useAuth()
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [documents, setDocuments] = useState<ChatDocument[]>(readStoredDocs)
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const transcriptRef = useRef<HTMLDivElement>(null)
  const newlyCreatedConversationId = useRef<string | null>(null)

  const setIsSendingBoth = useCallback((v: boolean) => {
    setIsSending(v)
    onIsSendingChange(v)
  }, [onIsSendingChange])

  // Load messages when conversation changes
  useEffect(() => {
    if (conversationId && newlyCreatedConversationId.current === conversationId) {
      newlyCreatedConversationId.current = null
      return
    }
    setMessages([])
    setError(null)
    if (!conversationId) return
    const controller = new AbortController()
    void authFetch(`${apiBaseUrl}/api/conversations/${conversationId}/messages`, { signal: controller.signal })
      .then(async res => {
        if (!res.ok) throw new Error()
        return res.json() as Promise<ChatMessage[]>
      })
      .then(msgs => setMessages(msgs))
      .catch(err => { if (err?.name !== 'AbortError') setError('Unable to load conversation history.') })
    return () => controller.abort()
  }, [conversationId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Auto-scroll to bottom
  useEffect(() => {
    transcriptRef.current?.scrollTo({ top: transcriptRef.current.scrollHeight, behavior: 'smooth' })
  }, [messages])

  async function createConversation() {
    const res = await authFetch(`${apiBaseUrl}/api/conversations`, { method: 'POST' })
    if (!res.ok) throw new Error('Unable to start a new conversation.')
    const data = await res.json() as { conversationId: string }
    newlyCreatedConversationId.current = data.conversationId
    onConversationCreated(data.conversationId)
    return data.conversationId
  }

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const content = draft.trim()
    if (!content || isSending) return
    setError(null); setDraft(''); setIsSendingBoth(true)
    const userMsg: ChatMessage = { id: crypto.randomUUID(), role: 'user', content, createdAt: new Date().toISOString() }
    const assistantId = crypto.randomUUID()
    setMessages(prev => [...prev, userMsg, { id: assistantId, role: 'assistant', content: '', createdAt: new Date().toISOString() }])
    try {
      const activeId = conversationId ?? await createConversation()
      const res = await authFetch(`${apiBaseUrl}/api/chat/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ conversationId: activeId, message: content, documents }),
      })
      if (!res.ok || !res.body) throw new Error('Unable to send your message.')
      await consumeStream(res.body, (eventName, data) => {
        if (eventName === 'token')
          setMessages(prev => prev.map(m => m.id === assistantId ? { ...m, content: m.content + String((data as { text?: unknown }).text ?? '') } : m))
        if (eventName === 'error')
          throw new Error(String((data as { message?: unknown }).message ?? 'The assistant could not respond.'))
      })
    } catch (err) {
      setMessages(prev => prev.filter(m => m.id !== assistantId))
      setError(err instanceof Error ? err.message : 'The assistant could not respond.')
    } finally {
      setIsSendingBoth(false)
    }
  }

  function addDocuments(e: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? [])
    void Promise.all(
      files.slice(0, 10 - documents.length).map(async f => ({ name: f.name, content: await f.text() }))
    ).then(added => {
      const next = [...documents, ...added.filter(d => d.content.length <= 100_000)]
      setDocuments(next)
      localStorage.setItem(DOCS_KEY, JSON.stringify(next))
    })
    e.target.value = ''
  }

  function removeDocument(name: string) {
    const next = documents.filter(d => d.name !== name)
    setDocuments(next)
    localStorage.setItem(DOCS_KEY, JSON.stringify(next))
  }

  return (
    <div className={styles.panel}>
      {/* Topbar */}
      <header className={styles.topbar}>
        <button className={styles.menuBtn} onClick={onMenuOpen} aria-label="Open sidebar">☰</button>
        <h2 className={styles.topbarTitle}>
          {conversationId ? 'Conversation' : 'New conversation'}
        </h2>
      </header>

      {/* Transcript */}
      <div className={styles.transcript} ref={transcriptRef} aria-live="polite">
        {messages.length === 0 ? (
          <div className={styles.emptyState}>
            <h2>Your conversation starts here.</h2>
            <p>Ask a question and the AI will respond as it writes.</p>
          </div>
        ) : (
          messages.map(msg => (
            <article key={msg.id} className={`${styles.message} ${styles[msg.role]}`}>
              <span className={styles.messageRole}>{msg.role === 'user' ? 'You' : 'Assistant'}</span>
              <div className={styles.messageBubble}>
                {msg.role === 'assistant' ? (
                  msg.content
                    ? <ReactMarkdown remarkPlugins={[remarkGfm]}>{msg.content}</ReactMarkdown>
                    : isSending ? <span className={styles.typing}>Thinking</span> : null
                ) : (
                  <p style={{ margin: 0 }}>{msg.content}</p>
                )}
              </div>
            </article>
          ))
        )}
      </div>

      {/* Composer */}
      <footer className={styles.composerArea}>
        {documents.length > 0 && (
          <div className={styles.documentList} aria-label="Attached documents">
            {documents.map(doc => (
              <span key={doc.name} className={styles.documentChip}>
                {doc.name}
                <button type="button" onClick={() => removeDocument(doc.name)} aria-label={`Remove ${doc.name}`}>×</button>
              </span>
            ))}
          </div>
        )}
        {error && <p className={styles.errorMsg} role="alert">{error}</p>}
        <form className={styles.composer} onSubmit={submit}>
          <label className="sr-only" htmlFor="message">Message</label>
          <textarea
            id="message"
            className={styles.textarea}
            value={draft}
            onChange={e => setDraft(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); e.currentTarget.form?.requestSubmit() }
            }}
            disabled={isSending}
            maxLength={8000}
            placeholder="Message the assistant…"
            rows={1}
          />
          <label className={styles.attachLabel} title="Attach text or Markdown documents">
            ＋
            <input type="file" accept=".txt,.md,.markdown,text/plain,text/markdown" multiple onChange={addDocuments} disabled={isSending || documents.length >= 10} />
          </label>
          <button className={styles.sendBtn} type="submit" disabled={isSending || !draft.trim()}>
            {isSending ? 'Sending…' : 'Send'}
          </button>
        </form>
      </footer>
    </div>
  )
}

async function consumeStream(
  stream: ReadableStream<Uint8Array>,
  onEvent: (name: string, data: unknown) => void
) {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const events = buffer.split('\n\n')
    buffer = events.pop() ?? ''
    for (const event of events) {
      const lines = event.split('\n')
      const name = lines.find(l => l.startsWith('event: '))?.slice(7)
      const payload = lines.find(l => l.startsWith('data: '))?.slice(6)
      if (name && payload) onEvent(name, JSON.parse(payload))
    }
  }
}
