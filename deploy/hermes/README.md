# Hermes Agent — AI assistant container

TransBrain ships an AI assistant for all users, powered by the self-learning
[Hermes Agent](https://hermes-agent.nousresearch.com/) by Nous Research running in its own
container (`nousresearch/hermes-agent`, started in gateway mode). The TransBrain API talks to its
OpenAI-compatible endpoint on port 8642 and proxies the chat for the two web frontends.

## Volume layout (`data/` → `/opt/data` in the container)

The `data/` directory is bind-mounted into the container as the agent's single state directory.
It is a persistent volume **partially checked into git**:

| Path                | In git | Purpose                                                              |
| ------------------- | ------ | -------------------------------------------------------------------- |
| `config.yaml`       | yes    | Hermes configuration (default model, turn limits, `approvals.mode: "off"`, `terminal.env_passthrough` for the `TRANSBRAIN_*` variables). Edit freely. |
| `SOUL.md`           | yes    | The assistant's identity and guardrails (German, TransBrain-specific).|
| `skills/transbrain-api/` | yes | Our curated skill teaching the agent the TransBrain REST API.    |
| `.env.example`      | yes    | Template for the admin-managed secrets below.                        |
| `.env`              | no     | LLM provider API keys + assistant switches. Written by the central administration UI (Verwaltung → KI-Assistent) or edited by hand. |
| everything else     | no     | Runtime state: the stock skills Hermes unpacks into `skills/` at startup, `sessions/`, `memories/`, `state.db`, locks, caches, … Stays in the volume (that is the "self-learning" part) but never in git — it contains user data. |

`data/.gitignore` is a **whitelist**: new runtime files are ignored automatically. If the agent
learns a skill worth keeping, add a `!skills/<name>/` + `!skills/<name>/**` pair there to adopt
it into git.

Hermes re-reads `.env` at the start of every turn, so API keys and switches saved through the
central administration take effect on the next message **without a container restart**. Changes
to `config.yaml` require a container restart.

Two `config.yaml` settings are load-bearing for the embedded setup: `approvals.mode: "off"`
(quoted — bare `off` is YAML for `false`), because the non-streaming chat integration has no
channel to answer Hermes approval prompts (a flagged command would stall the turn until timeout);
and `terminal.env_passthrough`, because Hermes sanitizes terminal environments and the
`transbrain-api` skill needs the `TRANSBRAIN_*` variables there (they are also declared as
`required_environment_variables` in the skill itself). Hermes' hardline blocklist for destructive
commands stays active even with approvals off.

## Configuration sources

* **Orchestrator-provided environment** (AppHost / docker-compose): infrastructure wiring —
  `API_SERVER_*` (the key the TransBrain API authenticates with), `TRANSBRAIN_API_BASE_URL`,
  `TRANSBRAIN_OIDC_TOKEN_URL`, `TRANSBRAIN_AGENT_CLIENT_ID/SECRET` for the skill.
* **`.env` in this volume**: everything an administrator manages at runtime — which LLM provider
  (OpenRouter by default; OpenAI, Anthropic, Google Gemini, DeepSeek, xAI or any custom
  OpenAI-compatible endpoint), the model, the provider API key, whether the assistant is enabled
  and whether it may act autonomously. The central administration UI writes exactly this file.

## Autonomy

The Keycloak dev realm defines a confidential client `transbrain-agent` whose service account has
the `disponent` role and a fixed demo tenant. The `transbrain-api` skill exchanges its client
credentials for a token and calls the TransBrain REST API with it — that is how the agent reads
live data and, when `TRANSBRAIN_ASSISTANT_AUTONOMY=true`, performs write actions on its own.
Remove the client (or the secret) to take that capability away entirely.

See `docs/KI_ASSISTENT.md` for the full picture.
