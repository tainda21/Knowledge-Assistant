import { useEffect, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { useAuth, apiBaseUrl } from '../contexts/AuthContext'
import styles from './AdminPanel.module.css'

interface UserRow {
  id: string
  username: string
  role: 'Admin' | 'User'
  createdAt: string
}

interface DocumentRow {
  id: string
  name: string
  content: string
  createdAt: string
  updatedAt: string
  sourceType: string
  sourceFileName: string | null
  ingestionStatus: 'Pending' | 'Processing' | 'Indexed' | 'Failed' | 'Deleting'
  ingestionError: string | null
  chunkCount: number
  indexedAt: string | null
}

interface Props {
  onMenuOpen: () => void
}

export default function AdminPanel({ onMenuOpen }: Props) {
  const { authFetch, user: currentUser } = useAuth()
  const [users, setUsers] = useState<UserRow[]>([])
  const [documents, setDocuments] = useState<DocumentRow[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [documentsLoading, setDocumentsLoading] = useState(true)

  const [newUsername, setNewUsername] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [newRole, setNewRole] = useState<'User' | 'Admin'>('User')
  const [isCreating, setIsCreating] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [formSuccess, setFormSuccess] = useState<string | null>(null)
  const [documentName, setDocumentName] = useState('')
  const [documentContent, setDocumentContent] = useState('')
  const [editingDocumentId, setEditingDocumentId] = useState<string | null>(null)
  const [isSavingDocument, setIsSavingDocument] = useState(false)
  const [documentError, setDocumentError] = useState<string | null>(null)
  const [documentSuccess, setDocumentSuccess] = useState<string | null>(null)

  useEffect(() => {
    void authFetch(`${apiBaseUrl}/api/users`)
      .then(async res => {
        if (!res.ok) throw new Error()
        return res.json() as Promise<UserRow[]>
      })
      .then(setUsers)
      .catch(() => {/* handled gracefully */})
      .finally(() => setIsLoading(false))
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    void authFetch(`${apiBaseUrl}/api/documents`)
      .then(async res => {
        if (!res.ok) throw new Error()
        return res.json() as Promise<DocumentRow[]>
      })
      .then(setDocuments)
      .catch(() => setDocumentError('Unable to load managed documents.'))
      .finally(() => setDocumentsLoading(false))
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleDelete(id: string, username: string) {
    if (!confirm(`Delete user "${username}"? This also deletes all their conversations.`)) return
    try {
      await authFetch(`${apiBaseUrl}/api/users/${id}`, { method: 'DELETE' })
      setUsers(prev => prev.filter(u => u.id !== id))
    } catch {
      alert('Failed to delete user.')
    }
  }

  async function handleCreate(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setFormError(null); setFormSuccess(null)
    if (!newUsername.trim() || newUsername.trim().length < 3) {
      setFormError('Username must be at least 3 characters.'); return
    }
    if (newPassword.length < 6) {
      setFormError('Password must be at least 6 characters.'); return
    }
    setIsCreating(true)
    try {
      const res = await authFetch(`${apiBaseUrl}/api/users`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: newUsername.trim(), password: newPassword, role: newRole }),
      })
      const body = await res.json() as UserRow & { message?: string }
      if (!res.ok) throw new Error(body.message ?? 'Failed to create user.')
      setUsers(prev => [body, ...prev])
      setNewUsername(''); setNewPassword(''); setNewRole('User')
      setFormSuccess(`User "${body.username}" created successfully.`)
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Failed to create user.')
    } finally {
      setIsCreating(false)
    }
  }

  function startEditDocument(document: DocumentRow) {
    setEditingDocumentId(document.id)
    setDocumentName(document.name)
    setDocumentContent(document.content)
    setDocumentError(null)
    setDocumentSuccess(null)
  }

  function resetDocumentForm() {
    setEditingDocumentId(null)
    setDocumentName('')
    setDocumentContent('')
  }

  async function handleSaveDocument(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setDocumentError(null); setDocumentSuccess(null)
    const name = documentName.trim()
    const content = documentContent.trim()
    if (!name || name.length > 255) { setDocumentError('Document name must contain 1 to 255 characters.'); return }
    if (!content || content.length > 100_000) { setDocumentError('Document content must contain 1 to 100000 characters.'); return }
    setIsSavingDocument(true)
    try {
      const isEditing = editingDocumentId !== null
      const res = await authFetch(`${apiBaseUrl}/api/documents${isEditing ? `/${editingDocumentId}` : ''}`, {
        method: isEditing ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, content }),
      })
      const body = await res.json() as DocumentRow & { message?: string }
      if (!res.ok) throw new Error(body.message ?? 'Failed to save document.')
      setDocuments(prev => isEditing ? prev.map(item => item.id === body.id ? body : item) : [body, ...prev])
      setDocumentSuccess(isEditing ? `Document "${body.name}" updated.` : `Document "${body.name}" added.`)
      resetDocumentForm()
    } catch (err) {
      setDocumentError(err instanceof Error ? err.message : 'Failed to save document.')
    } finally {
      setIsSavingDocument(false)
    }
  }

  async function handleDeleteDocument(document: DocumentRow) {
    if (!confirm(`Delete document "${document.name}"? It will no longer be used for retrieval.`)) return
    try {
      const res = await authFetch(`${apiBaseUrl}/api/documents/${document.id}`, { method: 'DELETE' })
      if (!res.ok) throw new Error()
      setDocuments(prev => prev.map(item => item.id === document.id ? { ...item, ingestionStatus: 'Deleting' } : item))
      if (editingDocumentId === document.id) resetDocumentForm()
    } catch {
      setDocumentError('Failed to delete document.')
    }
  }

  async function handleUploadDocument(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    setDocumentError(null); setDocumentSuccess(null); setIsSavingDocument(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const res = await authFetch(`${apiBaseUrl}/api/documents/upload`, { method: 'POST', body: form })
      const body = await res.json() as DocumentRow & { message?: string }
      if (!res.ok) throw new Error(body.message ?? 'Failed to upload document.')
      setDocuments(prev => [body, ...prev])
      setDocumentSuccess(`"${body.name}" was uploaded and queued for indexing.`)
    } catch (err) {
      setDocumentError(err instanceof Error ? err.message : 'Failed to upload document.')
    } finally {
      setIsSavingDocument(false)
    }
  }

  async function handleRetryDocument(document: DocumentRow) {
    try {
      const res = await authFetch(`${apiBaseUrl}/api/documents/${document.id}/retry`, { method: 'POST' })
      const body = await res.json() as DocumentRow & { message?: string }
      if (!res.ok) throw new Error(body.message ?? 'Failed to retry document.')
      setDocuments(prev => prev.map(item => item.id === body.id ? body : item))
    } catch (err) {
      setDocumentError(err instanceof Error ? err.message : 'Failed to retry document.')
    }
  }

  return (
    <div className={styles.panel}>
      <header className={styles.topbar}>
        <button className={styles.menuBtn} onClick={onMenuOpen} aria-label="Open sidebar">☰</button>
        <h2 className={styles.topbarTitle}>Admin Panel — Users &amp; Documents</h2>
      </header>

      <div className={styles.content}>
        {/* Users table */}
        <section className={styles.tableSection}>
          <h3 className={styles.sectionTitle}>All Users</h3>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Username</th>
                <th>Role</th>
                <th>Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr className={styles.emptyRow}><td colSpan={4}>Loading…</td></tr>
              ) : users.length === 0 ? (
                <tr className={styles.emptyRow}><td colSpan={4}>No users found.</td></tr>
              ) : users.map(u => (
                <tr key={u.id}>
                  <td>{u.username}</td>
                  <td>
                    <span className={`${styles.badge} ${u.role === 'Admin' ? styles.admin : styles.user}`}>
                      {u.role}
                    </span>
                  </td>
                  <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button
                      className={styles.deleteBtn}
                      onClick={() => handleDelete(u.id, u.username)}
                      disabled={u.id === currentUser?.id}
                      title={u.id === currentUser?.id ? 'Cannot delete yourself' : `Delete ${u.username}`}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        {/* Create user form */}
        <section className={styles.documentSection}>
          <div className={styles.sectionHeading}>
            <h3 className={styles.sectionTitle}>Managed Documents</h3>
            <span className={styles.sectionHint}>Used by assistant retrieval</span>
          </div>
          <table className={styles.table}>
            <thead><tr><th>Name</th><th>Status</th><th>Updated</th><th></th></tr></thead>
            <tbody>
              {documentsLoading ? <tr className={styles.emptyRow}><td colSpan={4}>Loading…</td></tr> : documents.length === 0 ? <tr className={styles.emptyRow}><td colSpan={4}>No documents added.</td></tr> : documents.map(document => (
                <tr key={document.id}>
                  <td><strong>{document.name}</strong><span className={styles.documentPreview}>{document.content ? `${document.content.slice(0, 100)}${document.content.length > 100 ? '…' : ''}` : document.sourceFileName}</span></td>
                  <td><span className={styles.status}>{document.ingestionStatus}</span>{document.ingestionError && <span className={styles.documentError}>{document.ingestionError}</span>}</td>
                  <td>{new Date(document.updatedAt).toLocaleDateString()}</td>
                  <td className={styles.actions}><button className={styles.editBtn} onClick={() => startEditDocument(document)} disabled={document.ingestionStatus === 'Deleting'}>Edit</button>{document.ingestionStatus === 'Failed' && <button className={styles.editBtn} onClick={() => void handleRetryDocument(document)}>Retry</button>}<button className={styles.deleteBtn} onClick={() => void handleDeleteDocument(document)} disabled={document.ingestionStatus === 'Deleting'}>Delete</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section className={styles.createSection}>
          <h3 className={styles.createTitle}>{editingDocumentId ? 'Edit Document' : 'Add Document'}</h3>
          {!editingDocumentId && <div className={styles.uploadRow}><label className={styles.uploadBtn}>Upload PDF, TXT, or Markdown<input type="file" accept=".pdf,.txt,.md,.markdown,application/pdf,text/plain,text/markdown" onChange={handleUploadDocument} disabled={isSavingDocument} /></label></div>}
          <form className={styles.documentForm} onSubmit={handleSaveDocument}>
            <div className={styles.field}><label className={styles.label} htmlFor="document-name">Name</label><input id="document-name" className={styles.input} value={documentName} onChange={e => setDocumentName(e.target.value)} placeholder="e.g. return-policy.md" disabled={isSavingDocument} /></div>
            <div className={styles.field}><label className={styles.label} htmlFor="document-content">Content</label><textarea id="document-content" className={styles.textarea} value={documentContent} onChange={e => setDocumentContent(e.target.value)} placeholder="Paste the knowledge content used for retrieval…" rows={8} disabled={isSavingDocument} /></div>
            <div className={styles.documentButtons}><button className={styles.createBtn} type="submit" disabled={isSavingDocument}>{isSavingDocument ? 'Saving…' : editingDocumentId ? 'Save Changes' : 'Add Document'}</button>{editingDocumentId && <button className={styles.cancelBtn} type="button" onClick={resetDocumentForm}>Cancel</button>}</div>
          </form>
          {documentError && <p className={styles.errorMsg} role="alert">{documentError}</p>}
          {documentSuccess && <p className={styles.successMsg}>{documentSuccess}</p>}
        </section>

        <section className={styles.createSection}>
          <h3 className={styles.createTitle}>Create New User</h3>
          <form className={styles.form} onSubmit={handleCreate}>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="new-username">Username</label>
              <input
                id="new-username"
                className={styles.input}
                type="text"
                value={newUsername}
                onChange={e => setNewUsername(e.target.value)}
                placeholder="e.g. jsmith"
                disabled={isCreating}
              />
            </div>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="new-password">Password</label>
              <input
                id="new-password"
                className={styles.input}
                type="password"
                value={newPassword}
                onChange={e => setNewPassword(e.target.value)}
                placeholder="Min. 6 characters"
                disabled={isCreating}
              />
            </div>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="new-role">Role</label>
              <select
                id="new-role"
                className={styles.select}
                value={newRole}
                onChange={e => setNewRole(e.target.value as 'User' | 'Admin')}
                disabled={isCreating}
              >
                <option value="User">User</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <button className={styles.createBtn} type="submit" disabled={isCreating}>
              {isCreating ? 'Creating…' : 'Create User'}
            </button>
          </form>
          {formError && <p className={styles.errorMsg} role="alert">{formError}</p>}
          {formSuccess && <p className={styles.successMsg}>{formSuccess}</p>}
        </section>
      </div>
    </div>
  )
}
