import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import { TICKET_CATEGORIES, type KnowledgeArticle, type KnowledgeSearchResult, type TicketCategory } from '../api/types'
import { categoryLabel, formatDateTime } from '../utils/format'

/** The knowledge base of past fixes that AI triage retrieves from. */
export default function KnowledgePage() {
  const [searchParams] = useSearchParams()
  const fromTicket = Number(searchParams.get('fromTicket')) || null

  const [articles, setArticles] = useState<KnowledgeArticle[] | null>(null)
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<KnowledgeSearchResult[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [showForm, setShowForm] = useState(fromTicket !== null)
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState<TicketCategory>('Other')
  const [problem, setProblem] = useState('')
  const [resolution, setResolution] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.listKnowledge().then(setArticles).catch((e: Error) => setError(e.message))
  }, [])

  // "Save fix to knowledge base" from a resolved ticket: prefill from the ticket and its last agent reply.
  useEffect(() => {
    if (!fromTicket) return
    api
      .getTicket(fromTicket)
      .then((t) => {
        setTitle(t.title)
        setCategory(t.category)
        setProblem(t.description)
        const lastAgentReply = [...t.comments].reverse().find((c) => c.authorRole === 'Agent' && !c.isInternal)
        setResolution(lastAgentReply?.body ?? '')
      })
      .catch((e: Error) => setError(e.message))
  }, [fromTicket])

  async function search(e: FormEvent) {
    e.preventDefault()
    if (!query.trim()) return setResults(null)
    try {
      setResults(await api.searchKnowledge(query))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  async function create(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const article = await api.createKnowledge({ title, category, problem, resolution, sourceTicketId: fromTicket })
      setArticles((a) => [article, ...(a ?? [])])
      setShowForm(false)
      setTitle('')
      setProblem('')
      setResolution('')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section>
      <div className="page-header">
        <div>
          <h1>Knowledge base</h1>
          <p className="muted">Past fixes. AI triage searches these to suggest solution steps for new tickets.</p>
        </div>
        <button className="button primary" onClick={() => setShowForm((s) => !s)}>
          {showForm ? 'Cancel' : 'Add article'}
        </button>
      </div>
      {error && <p className="error">{error}</p>}

      {showForm && (
        <form className="card form" onSubmit={create}>
          {fromTicket && <p className="muted small">From ticket #{fromTicket}. Edit it into a reusable fix before saving.</p>}
          <div className="form-row">
            <label>
              Title
              <input required minLength={5} maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} />
            </label>
            <label>
              Category
              <select value={category} onChange={(e) => setCategory(e.target.value as TicketCategory)}>
                {TICKET_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {categoryLabel(c)}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label>
            Problem (symptoms as a user would describe them)
            <textarea required minLength={10} rows={3} value={problem} onChange={(e) => setProblem(e.target.value)} />
          </label>
          <label>
            Resolution (what fixed it)
            <textarea required minLength={10} rows={3} value={resolution} onChange={(e) => setResolution(e.target.value)} />
          </label>
          <button className="button primary" disabled={busy}>
            Save article
          </button>
        </form>
      )}

      <form className="filters" onSubmit={search}>
        <input
          type="search"
          placeholder="Try a semantic search, e.g. “printer says offline”"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Search knowledge base"
        />
        <button className="button">Search</button>
      </form>

      {results && (
        <div className="card">
          <h3>Search results</h3>
          {results.length === 0 ? (
            <p className="muted">No sufficiently similar articles.</p>
          ) : (
            <ol className="search-results">
              {results.map((r) => (
                <li key={r.id}>
                  {r.title} <span className="muted small">· {categoryLabel(r.category)} · similarity {r.score.toFixed(2)}</span>
                </li>
              ))}
            </ol>
          )}
        </div>
      )}

      {!articles && !error && <p className="muted">Loading articles…</p>}
      <div className="kb-list">
        {articles?.map((a) => (
          <article key={a.id} className="card kb-article">
            <div className="kb-header">
              <h3>{a.title}</h3>
              <span className="tag">{categoryLabel(a.category)}</span>
            </div>
            <p>
              <strong>Problem:</strong> {a.problem}
            </p>
            <p>
              <strong>Fix:</strong> {a.resolution}
            </p>
            <p className="muted small">
              Added {formatDateTime(a.createdAt)}
              {a.sourceTicketId && (
                <>
                  {' '}
                  from <Link to={`/tickets/${a.sourceTicketId}`}>ticket #{a.sourceTicketId}</Link>
                </>
              )}
            </p>
          </article>
        ))}
      </div>
    </section>
  )
}
