# Valley Credit Union IT Ticketing System

A branch-aware IT ticketing web application for Valley Credit Union (Head Office + 6 Nova Scotia branches), implemented with:

- **Frontend:** React + TypeScript (Vite)
- **Backend:** ASP.NET Core Web API
- **Data Store (current implementation):** In-memory ticket store (can be swapped for SQL Server)

## Implemented Features

- Branch-aware ticket submission (Head Office + Branch1–Branch6)
- Priority levels with SLA due time calculation
  - Critical: 4h
  - High: 8h
  - Medium: 24h
  - Low: 48h
- Ticket status workflow (New, InProgress, Resolved, Closed)
- Dashboard summary:
  - Open tickets
  - Resolved/closed tickets
  - Distribution by branch
  - Distribution by priority
- Ticket filtering by branch, priority, and status
- Audit entries for creation and status changes
- Valley CU branding-inspired navy + gold UI styling

## Repository Structure

- `/backend/ITTicketing.Api` – ASP.NET Core API
- `/frontend` – React client
- `/ITTicketing.slnx` – solution file

## Run Locally

### 1) Start API

```bash
cd /tmp/workspace/Anaxagorius/IT_ticketing/backend/ITTicketing.Api
dotnet run
```

API runs at `http://localhost:5101`.

### 2) Start Frontend

```bash
cd /tmp/workspace/Anaxagorius/IT_ticketing/frontend
npm install
npm run dev
```

Frontend runs at `http://localhost:5173` and proxies `/api` calls to the backend.

## Validation Commands

### Backend

```bash
cd /tmp/workspace/Anaxagorius/IT_ticketing/backend/ITTicketing.Api
dotnet build
```

### Frontend

```bash
cd /tmp/workspace/Anaxagorius/IT_ticketing/frontend
npm run lint
npm run build
```

## Next Recommended Steps

- Replace in-memory store with SQL Server + EF Core migrations
- Add Active Directory / Azure AD authentication and role-based authorization
- Add email notifications for updates/escalations
- Add SLA breach alerts and escalation policies
- Add persistent audit/compliance reporting
