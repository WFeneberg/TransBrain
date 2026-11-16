---
name: transbrain-api
description: Read and change live TransBrain data (orders, tours, vehicles, drivers, customers, invoices) through the TransBrain REST API using the agent's own service account. Use whenever a user asks about current operational data or wants the assistant to perform an action in TransBrain.
required_environment_variables:
  - TRANSBRAIN_API_BASE_URL
  - TRANSBRAIN_OIDC_TOKEN_URL
  - TRANSBRAIN_AGENT_CLIENT_ID
  - TRANSBRAIN_AGENT_CLIENT_SECRET
  - TRANSBRAIN_ASSISTANT_AUTONOMY
---

# TransBrain REST API

## Configuration (environment)

| Variable | Meaning |
| --- | --- |
| `TRANSBRAIN_API_BASE_URL` | Base URL of the TransBrain API, e.g. `http://api:8080` |
| `TRANSBRAIN_OIDC_TOKEN_URL` | Keycloak token endpoint of the `transbrain` realm |
| `TRANSBRAIN_AGENT_CLIENT_ID` / `TRANSBRAIN_AGENT_CLIENT_SECRET` | Client credentials of the agent's service account (role `disponent`, fixed demo tenant) |
| `TRANSBRAIN_ASSISTANT_AUTONOMY` | `true` = self-initiated write actions are allowed too; instructed writes follow the per-conversation system context |

If any of these are missing, say so instead of guessing.

## 1. Get a token (cache it for ~4 minutes)

```sh
curl -sk -X POST "$TRANSBRAIN_OIDC_TOKEN_URL" \
  -d grant_type=client_credentials \
  -d client_id="$TRANSBRAIN_AGENT_CLIENT_ID" \
  -d client_secret="$TRANSBRAIN_AGENT_CLIENT_SECRET"
```

Take `access_token` from the JSON answer. `-k` is needed because the development Keycloak uses a
self-signed certificate; if the URL answers with a TLS/protocol error, retry with the scheme
flipped (`http` ↔ `https`) — dev setups differ.

## 2. Call the API

```sh
curl -s -H "Authorization: Bearer $TOKEN" "$TRANSBRAIN_API_BASE_URL/api/orders?page=1&pageSize=20"
```

## Python implementation (execute_code)

Use `subprocess.run()` instead of `hermes_tools.terminal()` for cleaner JSON parsing — terminal() can mix progress output even with `-s`, breaking json_parse().

```python
import os
import subprocess
from hermes_tools import json_parse

# Token
token_cmd = [
    'curl', '-sk', '-X', 'POST', os.environ['TRANSBRAIN_OIDC_TOKEN_URL'],
    '-d', 'grant_type=client_credentials',
    '-d', f'client_id={os.environ["TRANSBRAIN_AGENT_CLIENT_ID"]}',
    '-d', f'client_secret={os.environ["TRANSBRAIN_AGENT_CLIENT_SECRET"]}'
]
result = subprocess.run(token_cmd, capture_output=True, text=True, timeout=30)
token_data = json_parse(result.stdout)
access_token = token_data['access_token']

# API call
vehicles_cmd = [
    'curl', '-s',
    '-H', f'Authorization: Bearer {access_token}',
    f'{os.environ["TRANSBRAIN_API_BASE_URL"]}/api/vehicles'
]
result = subprocess.run(vehicles_cmd, capture_output=True, text=True, timeout=30)
data = json_parse(result.stdout)
```

Useful endpoints (all JSON, all paginated lists take `page`/`pageSize`, most take `search`):

| Area | Endpoints |
| --- | --- |
| Orders | `GET/POST /api/orders`, `GET/PUT /api/orders/{id}`, `POST /api/orders/{id}/cancel` |
| Tours | `GET/POST /api/tours`, `GET /api/tours/{id}`, `POST /api/tours/{id}/orders` (assign), `POST /api/tours/{id}/start`, `POST /api/tours/{id}/complete`, `GET /api/tours/{id}/fitting-orders` |
| Vehicles | `GET/POST /api/vehicles`, `GET/PUT/DELETE /api/vehicles/{id}`, `POST /api/vehicles/{id}/status` |
| Drivers | `GET/POST /api/drivers`, `GET/PUT/DELETE /api/drivers/{id}` |
| Customers | `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`, locations under `/api/customers/{id}/locations` |
| Invoices | `GET /api/invoices`, `GET /api/invoices/{id}`, `POST /api/invoices`, `POST /api/invoices/{id}/issue` |

Discover anything else via the OpenAPI document at `GET $TRANSBRAIN_API_BASE_URL/openapi/v1.json`
(development only).

## Rules

1. **Write gate:** whether you may `POST`/`PUT`/`DELETE` is stated in the system message of each
   conversation ("Kontext dieser Anfrage"): writes the user **clearly instructed** are allowed
   whenever that context says the user may instruct writes. `TRANSBRAIN_ASSISTANT_AUTONOMY=true`
   additionally allows **self-initiated** writes (your own sensible proposals, without a per-action
   instruction). When writing is not allowed, explain what you *would* do and how to do it in the
   UI instead.
2. Announce every write action in one short sentence before executing it, and report the result
   (including the id of anything you created).
3. On `403` you lack the role for that action — say so. On `503` the service behind the endpoint
   (routing, geocoding) is still starting.
4. Never print tokens, client secrets or raw `Authorization` headers into the chat.
