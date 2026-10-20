# Knowledge Assistant

A local RAG (Retrieval-Augmented Generation) chat app. Admins upload documents (PDF, TXT, Markdown) or paste text, and users chat with an assistant that answers using that knowledge base.

> Status: personal portfolio project. Source citations are not implemented yet.

## Features

- Login with JWT (admin and normal users)
- Streaming chat with saved conversation history
- Admin panel to manage users and documents
- Documents are chunked, embedded and stored in ChromaDB, then retrieved by semantic search when answering
- Temporary text/Markdown attachments in chat (used for that request only)

## How it works

```mermaid
flowchart LR
    UI["Frontend<br/>React + Vite"] -->|REST / SSE| API["WebAPI<br/>ASP.NET Core 8 + SQLite"]
    API -->|chat + indexing| AI["Assistant<br/>FastAPI + LangGraph"]
    AI --> DB[("ChromaDB")]
    AI --> LLM["Gemini API"]
```

- **Frontend**: login, chat, history sidebar, admin panel.
- **WebAPI**: auth, chat history and document records (SQLite), plus a background worker that sends documents to the Assistant for indexing.
- **Assistant**: embeds documents, searches ChromaDB, and streams answers from Gemini.

## Tech stack

React 19, TypeScript, Vite, ASP.NET Core 8, EF Core, SQLite, Python, FastAPI, LangGraph, ChromaDB, Google Gemini

## Run with Docker Compose

Docker Desktop is the only prerequisite for the containerized setup. The Compose stack builds and runs the Frontend, WebAPI, and Assistant services together.

### 1. Configure the environment

From the repository root, copy the example environment file and replace the placeholder values:

```powershell
Copy-Item .env.example .env
```

At minimum, set these values in `.env`:

```dotenv
GEMINI_API_KEY=your-gemini-api-key
ASSISTANT_INTERNAL_KEY=choose-a-private-shared-key
JWT_SECRET_KEY=generate-a-long-random-secret
ADMIN_PASSWORD=choose-a-strong-password
```

The root `.env` is local-only and is ignored by Git. Do not commit API keys, passwords, or signing keys.

### 2. Build and start the stack

```bash
docker compose up --build -d
```

Open the application at [http://localhost:5173](http://localhost:5173). The other service endpoints are:

- WebAPI Swagger: [http://localhost:5090/swagger](http://localhost:5090/swagger)
- Assistant health: [http://localhost:8000/health](http://localhost:8000/health)

The browser calls the WebAPI through `http://localhost:5090`; the WebAPI calls the Assistant internally through the Compose service name `assistant`.

### 3. Use and manage the stack

Log in with the `ADMIN_USERNAME` and `ADMIN_PASSWORD` values from `.env`, upload documents from **Admin Panel**, and start chatting after ingestion completes.

Useful commands:

```bash
docker compose ps
docker compose logs -f
docker compose logs -f assistant webapi
docker compose stop
docker compose down
```

SQLite data and the ChromaDB index are stored in named Docker volumes, so rebuilding images or running `docker compose down` does not remove application data. To intentionally reset all persisted data, run:

```bash
docker compose down -v
```

The `-v` option permanently removes the Compose volumes, including the database, users, conversations, uploaded documents, and vector index.

## Quick install

### Prerequisites
- Node.js 20+
- .NET SDK 8
- Python 3.10 - 3.12
- A [Gemini API key](https://aistudio.google.com/)

Start the services in order (each in its own terminal):

### 1. Assistant (Python AI Service - port 8000)

```bash
cd Assistant
python -m venv .venv
# Windows (PowerShell): .\.venv\Scripts\Activate.ps1
# Linux/macOS:          source .venv/bin/activate

pip install -r requirements.txt
```

The Assistant loads its settings from the shared `.env` file in the repository root. If you have not already created it, run this from the repository root:

```powershell
Copy-Item .env.example .env
```

Configure the AI-service values in the root `.env` file:

```dotenv
GEMINI_API_KEY=your-gemini-api-key
ASSISTANT_INTERNAL_KEY=choose-a-private-shared-key
```

You can also customize `GEMINI_MODEL`, `EMBEDDING_MODEL`, `CHROMA_PERSIST_DIR`,
`ASSISTANT_HOST`, `ASSISTANT_PORT`, and `LOG_LEVEL` in the same root file.

Start the server:
```bash
uvicorn main:app --port 8000 --reload
```

### 2. WebAPI (ASP.NET Core - port 5090)

WebAPI settings are configured through ASP.NET Core environment variables. Set
custom values before starting the API; use double underscores (`__`) for nested
settings:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=knowledge_assistant.db"
$env:AssistantService__BaseUrl = "http://localhost:8000"
$env:AssistantService__InternalKey = "choose-the-same-key-as-ASSISTANT_INTERNAL_KEY"
$env:Frontend__Origin = "http://localhost:5173"
$env:Jwt__SecretKey = "generate-a-long-random-secret"
$env:AdminSeed__Username = "admin"
$env:AdminSeed__Password = "choose-a-strong-password"
dotnet run
```

On macOS/Linux, use `export NAME=value` instead of `$env:NAME = value`.
These variables override values in `WebAPI/appsettings.json`, so do not put
real API keys, passwords, or signing keys in that file. The internal key must
match `ASSISTANT_INTERNAL_KEY` in the root `.env`. For repeatable local setup,
put the PowerShell values in your local shell profile or use the .NET user
secrets store; do not commit a secrets file.

```bash
cd WebAPI
dotnet run
```

> **Note:** On first startup, SQLite database migrations are applied automatically, and a default admin account is seeded using `AdminSeed__Username` and `AdminSeed__Password`. Swagger UI is available at `http://localhost:5090/swagger`.

### 3. Frontend (React + Vite - port 5173)

```bash
cd Frontend
npm install
npm run dev
```

The frontend connects to WebAPI at `http://localhost:5090` by default. To use a
custom API URL, add this value to the root `.env` file:

```dotenv
VITE_API_BASE_URL=http://localhost:5090
```

Open [http://localhost:5173](http://localhost:5173). Do not put private keys or
passwords in `VITE_*` variables; Vite exposes them to the browser.

---

### First-time login & usage

1. **Log in with the seeded admin account**:
   - Use the values you set with `AdminSeed__Username` and `AdminSeed__Password`.
   - Set the password before the first WebAPI startup; changing it later does not
     change an account that has already been created.
2. **Upload documents**: Navigate to **Admin Panel** to upload `.pdf`, `.txt`, or `.md` files. The background worker will extract, chunk, and index them into ChromaDB.
3. **Start chatting**: Create a conversation and ask questions based on the ingested knowledge base.


## Project structure

```text
Assistant/   Python AI service (LangGraph, Gemini, ChromaDB)
WebAPI/      ASP.NET Core gateway (auth, chat history, documents, ingestion worker)
Frontend/    React client
```

## Tests

```bash
cd Assistant && pytest -v tests/
cd Frontend && npm run lint && npm run build
```

The WebAPI has no test project yet.

## Known limitations

- No source citations in answers
- Scanned PDFs (no extractable text) can't be indexed
