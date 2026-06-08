import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type BranchLocation =
  | 'HeadOffice'
  | 'Branch1'
  | 'Branch2'
  | 'Branch3'
  | 'Branch4'
  | 'Branch5'
  | 'Branch6'

type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'
type TicketStatus = 'New' | 'InProgress' | 'Resolved' | 'Closed'

type Ticket = {
  id: string
  title: string
  description: string
  branch: BranchLocation
  priority: TicketPriority
  status: TicketStatus
  submittedBy: string
  assignedTo?: string | null
  createdAt: string
  updatedAt: string
  dueBy: string
}

type TicketSummary = {
  totalOpen: number
  totalResolved: number
  byBranch: Record<string, number>
  byPriority: Record<string, number>
}

type ComplianceSummary = {
  totalBreaches: number
  activeBreaches: number
  activeEscalations: number
  totalEscalations: number
  meanResolutionHours: number
  notificationSuccessRate: number
  breachesByPriority: Record<string, number>
}

const branches: BranchLocation[] = [
  'HeadOffice',
  'Branch1',
  'Branch2',
  'Branch3',
  'Branch4',
  'Branch5',
  'Branch6',
]

const priorities: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical']
const statuses: TicketStatus[] = ['New', 'InProgress', 'Resolved', 'Closed']

const defaultSummary: TicketSummary = {
  totalOpen: 0,
  totalResolved: 0,
  byBranch: {},
  byPriority: {},
}

const defaultComplianceSummary: ComplianceSummary = {
  totalBreaches: 0,
  activeBreaches: 0,
  activeEscalations: 0,
  totalEscalations: 0,
  meanResolutionHours: 0,
  notificationSuccessRate: 0,
  breachesByPriority: {},
}

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

async function parseJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`)
  }

  return (await response.json()) as T
}

function App() {
  const [tickets, setTickets] = useState<Ticket[]>([])
  const [summary, setSummary] = useState<TicketSummary>(defaultSummary)
  const [complianceSummary, setComplianceSummary] =
    useState<ComplianceSummary>(defaultComplianceSummary)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshTick, setRefreshTick] = useState(0)

  const [filterBranch, setFilterBranch] = useState<string>('')
  const [filterPriority, setFilterPriority] = useState<string>('')
  const [filterStatus, setFilterStatus] = useState<string>('')

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [submitter, setSubmitter] = useState('')
  const [branch, setBranch] = useState<BranchLocation>('HeadOffice')
  const [priority, setPriority] = useState<TicketPriority>('Medium')

  const queryString = useMemo(() => {
    const params = new URLSearchParams()
    if (filterBranch) params.set('branch', filterBranch)
    if (filterPriority) params.set('priority', filterPriority)
    if (filterStatus) params.set('status', filterStatus)
    const query = params.toString()
    return query.length > 0 ? `?${query}` : ''
  }, [filterBranch, filterPriority, filterStatus])

  useEffect(() => {
    let isCancelled = false

    const fetchData = async () => {
      try {
        const [ticketResponse, summaryResponse, complianceSummaryResponse] =
          await Promise.all([
            fetch(`${API_BASE}/api/tickets${queryString}`),
            fetch(`${API_BASE}/api/tickets/summary`),
            fetch(`${API_BASE}/api/compliance/summary`),
          ])

        const [ticketData, summaryData, complianceData] = await Promise.all([
          parseJson<Ticket[]>(ticketResponse),
          parseJson<TicketSummary>(summaryResponse),
          parseJson<ComplianceSummary>(complianceSummaryResponse),
        ])

        if (!isCancelled) {
          setTickets(ticketData)
          setSummary(summaryData)
          setComplianceSummary(complianceData)
        }
      } catch (requestError) {
        if (!isCancelled) {
          setError(
            requestError instanceof Error
              ? requestError.message
              : 'Unable to load data.',
          )
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void fetchData()

    return () => {
      isCancelled = true
    }
  }, [queryString, refreshTick])

  async function createTicket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      const response = await fetch(`${API_BASE}/api/tickets`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          title,
          description,
          branch,
          priority,
          submittedBy: submitter,
        }),
      })

      await parseJson<Ticket>(response)

      setTitle('')
      setDescription('')
      setSubmitter('')
      setBranch('HeadOffice')
      setPriority('Medium')
      setRefreshTick((current) => current + 1)
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : 'Unable to create ticket.',
      )
    }
  }

  async function updateStatus(ticketId: string, status: TicketStatus) {
    setError(null)
    setIsLoading(true)
    try {
      const response = await fetch(`${API_BASE}/api/tickets/${ticketId}/status`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          status,
          updatedBy: 'IT Staff',
        }),
      })

      await parseJson<Ticket>(response)
      setRefreshTick((current) => current + 1)
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : 'Unable to update status.',
      )
    }
  }

  async function updateTicketDetails(ticketId: string, details: Partial<{ priority: TicketPriority; assignedTo: string | null }>) {
    setError(null)
    setIsLoading(true)

    try {
      const response = await fetch(`${API_BASE}/api/tickets/${ticketId}/details`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          ...details,
          updatedBy: 'IT Staff',
        }),
      })

      await parseJson<Ticket>(response)
      setRefreshTick((current) => current + 1)
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : 'Unable to update ticket details.',
      )
    }
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <h1>Valley Credit Union IT Ticketing</h1>
        <p>
          Centralized support for Head Office and six Nova Scotia branch locations.
        </p>
      </header>

      <section className="metrics-grid">
        <article className="metric-card">
          <h2>Open Tickets</h2>
          <p>{summary.totalOpen}</p>
        </article>
        <article className="metric-card">
          <h2>Resolved / Closed</h2>
          <p>{summary.totalResolved}</p>
        </article>
        <article className="metric-card">
          <h2>Active SLA Breaches</h2>
          <p>{complianceSummary.activeBreaches}</p>
        </article>
        <article className="metric-card">
          <h2>Active Escalations</h2>
          <p>{complianceSummary.activeEscalations}</p>
        </article>
      </section>

      <section className="metrics-grid secondary-metrics">
        <article className="metric-card compact">
          <h2>Total Escalations</h2>
          <p>{complianceSummary.totalEscalations}</p>
        </article>
        <article className="metric-card compact">
          <h2>Notification Health</h2>
          <p>{Math.round(complianceSummary.notificationSuccessRate * 100)}%</p>
        </article>
        <article className="metric-card compact">
          <h2>Mean Resolution (hrs)</h2>
          <p>{complianceSummary.meanResolutionHours}</p>
        </article>
      </section>

      <section className="content-grid">
        <article className="panel">
          <h2>Submit Ticket</h2>
          <form onSubmit={createTicket} className="ticket-form">
            <label>
              Title
              <input
                required
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </label>
            <label>
              Description
              <textarea
                required
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </label>
            <label>
              Requester
              <input
                value={submitter}
                onChange={(event) => setSubmitter(event.target.value)}
                placeholder="Employee name"
              />
            </label>
            <label>
              Branch
              <select
                value={branch}
                onChange={(event) => setBranch(event.target.value as BranchLocation)}
              >
                {branches.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Priority
              <select
                value={priority}
                onChange={(event) => setPriority(event.target.value as TicketPriority)}
              >
                {priorities.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <button type="submit">Create Ticket</button>
          </form>
        </article>

        <article className="panel">
          <h2>Filters</h2>
          <div className="filters">
            <label>
              Branch
              <select
                value={filterBranch}
                onChange={(event) => setFilterBranch(event.target.value)}
              >
                <option value="">All</option>
                {branches.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Priority
              <select
                value={filterPriority}
                onChange={(event) => setFilterPriority(event.target.value)}
              >
                <option value="">All</option>
                {priorities.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Status
              <select
                value={filterStatus}
                onChange={(event) => setFilterStatus(event.target.value)}
              >
                <option value="">All</option>
                {statuses.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <h3>By Branch</h3>
          <ul className="inline-list">
            {Object.entries(summary.byBranch).map(([name, count]) => (
              <li key={name}>
                {name}: <strong>{count}</strong>
              </li>
            ))}
          </ul>

          <h3>Breaches by Priority</h3>
          <ul className="inline-list">
            {Object.entries(complianceSummary.breachesByPriority).map(([name, count]) => (
              <li key={name}>
                {name}: <strong>{count}</strong>
              </li>
            ))}
          </ul>
        </article>
      </section>

      <section className="panel">
        <h2>Tickets</h2>
        {error ? <p className="error">{error}</p> : null}
        {isLoading ? <p>Loading...</p> : null}
        {!isLoading && tickets.length === 0 ? <p>No tickets found.</p> : null}

        {tickets.length > 0 ? (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Branch</th>
                  <th>Priority</th>
                  <th>Status</th>
                  <th>Assigned To</th>
                  <th>Submitted By</th>
                  <th>Due By</th>
                </tr>
              </thead>
              <tbody>
                {tickets.map((ticket) => (
                  <tr key={ticket.id}>
                    <td>
                      <strong>{ticket.title}</strong>
                      <p>{ticket.description}</p>
                    </td>
                    <td>{ticket.branch}</td>
                    <td>
                      <select
                        value={ticket.priority}
                        onChange={(event) =>
                          void updateTicketDetails(ticket.id, {
                            priority: event.target.value as TicketPriority,
                          })
                        }
                      >
                        {priorities.map((item) => (
                          <option key={item} value={item}>
                            {item}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <select
                        value={ticket.status}
                        onChange={(event) =>
                          void updateStatus(ticket.id, event.target.value as TicketStatus)
                        }
                      >
                        {statuses.map((item) => (
                          <option key={item} value={item}>
                            {item}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        defaultValue={ticket.assignedTo ?? ''}
                        placeholder="Unassigned"
                        onBlur={(event) =>
                          void updateTicketDetails(ticket.id, {
                            assignedTo: event.target.value || null,
                          })
                        }
                      />
                    </td>
                    <td>{ticket.submittedBy}</td>
                    <td>{new Date(ticket.dueBy).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>
    </div>
  )
}

export default App
