# 📚 Learn This Project

Interview-prep presentations explaining the project at **three levels**. Open them in a browser — they're self-contained HTML slide decks (use ↓ / ↑ or PageDown / PageUp to move between slides; each ends with interview Q&A).

**👉 Start at [`index.html`](./index.html)** — the landing page that links all three.

| Level | File | For when you need to… |
|---|---|---|
| 🟢 Easy | [`01-easy.html`](./01-easy.html) | Explain *what* this project is and does, in plain language |
| 🟡 Medium | [`02-medium.html`](./02-medium.html) | Explain *how* the pieces fit — architecture + the real-time flow |
| 🔴 Hard | [`03-hard.html`](./03-hard.html) | Defend the *why* behind every design decision and handle deep follow-ups |

## The one-sentence pitch (memorize this)

> "It's a real-time order dashboard where an ASP.NET Core API pushes live updates to a React client over a **SignalR** WebSocket, and a **Redis pub/sub** channel decouples the broadcast from the HTTP request that triggered it so it can scale across multiple servers."

## The 30-second flow (memorize this too)

1. A user places an order → `POST /api/orders`.
2. The API saves it and **publishes an event to Redis**, then returns `201`.
3. A **background worker** subscribed to that Redis channel receives the event.
4. The worker **broadcasts** over SignalR to every connected dashboard.
5. The React UI updates instantly — **no polling**.
