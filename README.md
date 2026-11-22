# Field Data Capture Platform

An end-to-end operations platform that demonstrates device data ingestion, real-time visualization, search and quality review, snapshot backup and restore, and cloud-ready deployment.

## Features

- REST API for event ingestion
- SignalR real-time dashboard updates
- Device and metric simulation
- Search and status filtering
- Approve and reject quality reviews
- JSON snapshot backup and restore
- Docker deployment configuration
- GitHub Actions CI workflow

## Run Locally

```powershell
dotnet run
```

Open `http://localhost:5000` or the URL printed by the .NET CLI.

## Run with Docker

```powershell
docker compose up --build
```

Open `http://localhost:8080`.

## Main API Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/health` | Health check |
| POST | `/api/events` | Ingest one event |
| GET | `/api/events` | Query and filter events |
| POST | `/api/events/{id}/review` | Approve or reject an event |
| POST | `/api/simulator` | Generate demo events |
| POST | `/api/backups` | Create a snapshot |
| GET | `/api/backups` | List snapshots |
| POST | `/api/backups/{id}/restore` | Restore a snapshot |
| POST | `/api/demo/reset` | Reset demo events |

## Example Ingestion Request

```json
{
  "deviceId": "sensor-north",
  "eventType": "measurement",
  "metric": "temperature",
  "value": 42.5,
  "unit": "C"
}
```
