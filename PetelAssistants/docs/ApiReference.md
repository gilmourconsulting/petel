# PetelMeitar — API Reference

REST API exposed by `PetelMeitar.Web`. All routes are under `/api`.

**Base URL (local development):** `http://localhost:5105/api`

**Interactive explorer:** `http://localhost:5105/swagger` (Development only)

---

## Response format

Every endpoint returns the same envelope:

```json
{
  "success": true,
  "message": "OK",
  "data": { }
}
```

On failure, `success` is `false`, `message` describes the error, and `data` is `null`.

| HTTP status | Meaning |
|---|---|
| `200` | Success |
| `201` | Created |
| `202` | Accepted (background job enqueued) |
| `400` | Bad request (validation error) |
| `404` | Resource not found |
| `409` | Conflict (e.g. duplicate symbol) |

---

## Beneficiaries

### List beneficiaries

```
GET /api/beneficiaries
```

Returns all beneficiaries from the database, ordered by name.

**Example (curl):**

```cmd
curl http://localhost:5105/api/beneficiaries
```

**Example (PowerShell):**

```powershell
Invoke-RestMethod http://localhost:5105/api/beneficiaries
```

---

## Tracked symbols

Tracked symbols are beneficiary codes configured for automated import.

### List symbols

```
GET /api/tracked-symbols
```

### Add a symbol

```
POST /api/tracked-symbols
Content-Type: application/json
```

**Body:**

```json
{
  "symbolCode": "10413516",
  "displayName": "My authority",
  "isActive": true
}
```

`symbolCode` is required. Returns `409` if the symbol already exists.

**Example:**

```powershell
$body = @{
  symbolCode  = '10413516'
  displayName = 'My authority'
  isActive    = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://localhost:5105/api/tracked-symbols' `
  -Method Post -ContentType 'application/json' -Body $body
```

### Activate or deactivate a symbol

```
PUT /api/tracked-symbols/{code}/active
Content-Type: application/json
```

**Body:** `true` or `false` (raw JSON boolean)

**Example:**

```powershell
Invoke-RestMethod -Uri 'http://localhost:5105/api/tracked-symbols/10413516/active' `
  -Method Put -ContentType 'application/json' -Body 'true'
```

### Delete a symbol

```
DELETE /api/tracked-symbols/{code}
```

Removes the symbol from the tracking list. Historical import data is not deleted.

---

## Import runs

### List import runs

```
GET /api/import-runs
```

| Query param | Type | Description |
|---|---|---|
| `beneficiaryCode` | string | Filter by beneficiary/symbol code |
| `status` | string | `Pending`, `Running`, `Succeeded`, or `Failed` |
| `limit` | int | Max rows to return (default: 50) |

Results are ordered by `startedAt` descending.

**Example — last 10 failed runs for one symbol:**

```cmd
curl "http://localhost:5105/api/import-runs?beneficiaryCode=10413516&status=Failed&limit=10"
```

### Get a single import run

```
GET /api/import-runs/{id}
```

`{id}` is a GUID.

### Retry an import

```
POST /api/import-runs/{id}/retry
```

Enqueues a new import job for all active symbols. Returns `202 Accepted`.

### Import a month range

```
POST /api/import-runs/range
Content-Type: application/json
```

Downloads and ingests data for every month in the given range.

**Body:**

```json
{
  "symbolCode": "10413516",
  "fromMonth": "01/2026",
  "toMonth": "06/2026"
}
```

| Field | Required | Description |
|---|---|---|
| `symbolCode` | No | Single symbol to import. Omit or set `null` to import all active symbols. |
| `fromMonth` | Yes | Start month in `MM/yyyy` format |
| `toMonth` | Yes | End month in `MM/yyyy` format |

Returns `202 Accepted` when the job is enqueued.

**Example:**

```powershell
$body = @{
  symbolCode = '10413516'
  fromMonth  = '01/2026'
  toMonth    = '06/2026'
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://localhost:5105/api/import-runs/range' `
  -Method Post -ContentType 'application/json' -Body $body
```

---

## Amounts query

Aggregated summary from the `mutavim` core table.

```
GET /api/amounts
```

| Query param | Type | Description |
|---|---|---|
| `beneficiaryCode` | string | Filter by beneficiary code |
| `fromMonth` | string | Start month (`MM/yyyy`) |
| `toMonth` | string | End month (`MM/yyyy`) |
| `topic` | string | Match `topicCode` exactly or `topicDescription` contains |

**Example:**

```cmd
curl "http://localhost:5105/api/amounts?beneficiaryCode=10413516&fromMonth=01/2026&toMonth=06/2026"
```

**Response `data` shape (array):**

```json
[
  {
    "beneficiaryCode": "10413516",
    "calcDate": "2026-01-01",
    "topicCode": "101",
    "topicDescription": "...",
    "totalCalculated": 12345.67,
    "rowCount": 3
  }
]
```

---

## Data query

Pull raw rows from any core data table, filtered by symbol list and a field value list.

```
POST /api/data/query
Content-Type: application/json
```

**Body:**

```json
{
  "symbolList": ["10413516", "10413517"],
  "fileName": "MUTAVIM",
  "filterField": "TopicCode",
  "filterValueList": ["101", "205"]
}
```

| Field | Required | Description |
|---|---|---|
| `symbolList` | Yes | One or more beneficiary/symbol codes (`BeneficiaryCode IN (...)`) |
| `fileName` | Yes | CSV type suffix — selects which core table to query (see table below) |
| `filterField` | Yes | Property name on the entity to filter by (case-insensitive) |
| `filterValueList` | Yes | Values to match (`filterField IN (...)`) |

Results are ordered by `calcDate` descending, capped at **1000 rows**.

**Response `data` shape:**

```json
{
  "fileName": "MUTAVIM",
  "rowCount": 42,
  "rows": [
    {
      "id": 1,
      "beneficiaryCode": "10413516",
      "calcDate": "2026-06-01",
      "topicCode": "101",
      "topicDescription": "...",
      "calculatedAmount": 5000.00
    }
  ]
}
```

### Supported file names

`fileName` is the CSV type suffix (not a full filename). Accepted values:

| Suffix | Data |
|---|---|
| `MUTAVIM` | Beneficiary budget topics |
| `CHESHBONIT` | Invoice summary |
| `SACAL` | Tuition |
| `SACALCHARIGIM` | Special tuition |
| `HASAOT` | Transport |
| `MUCARIM` | Recognised institutions |
| `AZAROLIM` | Azarolim |
| `GY003` | GY003 |
| `GY019` | GY019 |
| `GY033` | GY033 |
| `HASMASLULIM` | Transport routes |
| `HASNET` | Hasnet |
| `ICHLUSKITOT` | Class inclusion |
| `MISROT` | Positions |
| `MISROTGY` | GY positions |
| `MOADON` | Clubs |
| `SHARATIM` | Services |
| `SHEFI` | Shefi |
| `YADANIIM` | Manual entries |

### Supported filter field types

`filterField` must match a public property on the target entity. Supported types:

- `string`
- `int`, `long`, `decimal`, `double`, `float`, `bool`
- `Guid`
- `DateOnly` (use `MM/yyyy` or ISO date)
- `DateTime`

All filter values are passed as strings in the JSON body and parsed to the property type.

### Examples

**Query MUTAVIM rows for one symbol and topic:**

```powershell
$body = @{
  symbolList      = @('10413516')
  fileName        = 'MUTAVIM'
  filterField     = 'TopicCode'
  filterValueList = @('101', '205')
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://localhost:5105/api/data/query' `
  -Method Post -ContentType 'application/json' -Body $body
```

**Filter by calc month:**

```powershell
$body = @{
  symbolList      = @('10413516')
  fileName        = 'MUTAVIM'
  filterField     = 'CalcDate'
  filterValueList = @('06/2026')
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://localhost:5105/api/data/query' `
  -Method Post -ContentType 'application/json' -Body $body
```

### Common errors

| Message | Cause |
|---|---|
| `symbolList is required and must not be empty.` | Missing or empty `symbolList` |
| `Unknown file name suffix '...'.` | `fileName` is not a recognised CSV suffix |
| `Unknown filter field '...' for file type.` | Property does not exist on the target entity |
| `Invalid value '...' for filter field '...'` | Value cannot be parsed to the field's type |

---

## Quick reference

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/beneficiaries` | List beneficiaries |
| `GET` | `/api/tracked-symbols` | List tracked symbols |
| `POST` | `/api/tracked-symbols` | Add a symbol |
| `PUT` | `/api/tracked-symbols/{code}/active` | Toggle symbol active state |
| `DELETE` | `/api/tracked-symbols/{code}` | Remove a symbol |
| `GET` | `/api/import-runs` | List import runs |
| `GET` | `/api/import-runs/{id}` | Get one import run |
| `POST` | `/api/import-runs/{id}/retry` | Retry import (all active symbols) |
| `POST` | `/api/import-runs/range` | Import a month range |
| `GET` | `/api/amounts` | Aggregated mutavim amounts |
| `POST` | `/api/data/query` | Query core table rows with filters |

---

## Month format

Wherever a month is expected (`fromMonth`, `toMonth`, or `CalcDate` filter values), use **`MM/yyyy`**:

```
01/2026
06/2026
12/2025
```
