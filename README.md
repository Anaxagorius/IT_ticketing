# Valley Credit Union IT Ticketing System

A branch-aware IT ticketing web application for Valley Credit Union (Head Office + 6 Nova Scotia branches), implemented with:

- **Frontend:** React + TypeScript (Vite)
- **Backend:** ASP.NET Core Web API
- **Data Store:** SQL Server with Entity Framework Core migrations

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
- Assignment and priority update workflow with event tracking
- Notification and escalation configuration APIs (`/api/admin/*`)
- SLA warning/breach monitoring with escalation stages
- Compliance reporting APIs (`/api/compliance/summary`, `/api/compliance/export`)
- Valley CU branding-inspired navy + gold UI styling

## Repository Structure

- `/backend/ITTicketing.Api` – ASP.NET Core API
- `/frontend` – React client
- `/ITTicketing.slnx` – solution file

## Run Locally

### 0) Start SQL Server

Ensure SQL Server is running and update the `ConnectionStrings:DefaultConnection` value in (for SQL authentication, replace `Integrated Security=True` with explicit user/password settings):

- `backend/ITTicketing.Api/appsettings.json`
- `backend/ITTicketing.Api/appsettings.Development.json`

### 1) Start API

```bash
cd /tmp/workspace/Anaxagorius/IT_ticketing/backend/ITTicketing.Api
dotnet run
```

API runs at `http://localhost:5101` and applies EF Core migrations automatically on startup.

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

- Add Active Directory / Azure AD authentication and role-based authorization
- Configure Teams/Outlook webhook endpoints in backend appsettings
- Add authentication/authorization for admin and compliance endpoints
- Add automated tests for SLA monitoring and escalation transitions
