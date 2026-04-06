# 05_01_agent_graph

Port [`i-am-alice/4th-devs/05_01_agent_graph`](https://github.com/i-am-alice/4th-devs/tree/main/05_01_agent_graph) na C# / .NET Framework 4.8.

## Opis

Multi-agent graph — system wielu agentów z grafowym harmonogramem zadań i dashboardem w przeglądarce.

Orkiestrator tworzy specjalistów (researcher, writer, email_writer), deleguje im podzadania,
a scheduler automatycznie zarządza zależnościami, retry i wznowieniami.
Pamięć obserwacyjna (Observer/Reflector) kompresuje starszą historię, aby utrzymać kontekst w budżecie tokenów.

### Architektura

```
User request
    │
    ▼
┌─────────────────┐
│  Orchestrator    │  tworzy aktorów, deleguje zadania, łączy wyniki
└───────┬─────────┘
        │ delegate_task / create_actor
    ┌───┴───┬───────┐
    ▼       ▼       ▼
┌──────┐ ┌──────┐ ┌────────────┐
│Resear│ │Writer│ │Email Writer │
│cher  │ │      │ │            │
└──────┘ └──────┘ └────────────┘
    │       │           │
    ▼       ▼           ▼
 Artifacts  Artifacts   Emails
    │       │           │
    └───────┴───────────┘
              │
     ┌────────┴────────┐
     │    Scheduler     │  round-robin, dependency graph, auto-retry
     └────────┬────────┘
              │
     ┌────────┴────────┐
     │     Memory       │  observer → reflector → compressed observations
     └─────────────────┘
```

### Narzędzia agentów

| Narzędzie | Opis |
|-----------|------|
| `create_actor` | Tworzy lub aktualizuje specjalistę (z rejestru lub ad-hoc) |
| `delegate_task` | Tworzy podzadanie przypisane do istniejącego aktora |
| `complete_task` | Oznacza bieżące zadanie jako ukończone |
| `block_task` | Blokuje zadanie (brak możliwości kontynuacji) |
| `read_artifact` | Czyta istniejący artefakt po id lub ścieżce |
| `write_artifact` | Zapisuje/aktualizuje plik artefaktu |
| `send_email` | Tworzy e-mail (zapisywany jako artefakt Markdown) |

### Dashboard

Serwer HTTP na porcie **3300** serwuje:
- **Dashboard** (SSE real-time): pipeline agentów, feed zdarzeń, graf Cytoscape, panele szczegółów
- **API** `/api/state` — pełny stan grafu encji
- **API** `/api/artifact/{path}` — treść artefaktu

## Konfiguracja

```powershell
copy ..\..\App.example.config App.config
# Uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY
```

Opcjonalne zmienne środowiskowe:
- `PRIMITIVES_MODEL` — model LLM (domyślnie `gpt-4.1`)
- `PRIMITIVES_MAX_OUTPUT_TOKENS` — limit tokenów wyjściowych (domyślnie `16000`)

## Uruchamianie

```powershell
# Domyślne zadanie (blog post o TypeScript 5.0):
dotnet run --project 05_01_agent_graph.csproj

# Własne zadanie:
dotnet run --project 05_01_agent_graph.csproj -- "Write a comprehensive blog post about AI agents"
```

Dashboard otworzy się automatycznie w przeglądarce pod adresem `http://localhost:3300/`.

## Budowanie

```powershell
dotnet build 05_01_agent_graph.csproj
```

## Struktura projektu

```
05_01_agent_graph/
  Models/
    Domain.cs             ← Encje: Session, Actor, AgentTask, Item, Artifact, Relation
  Store/
    FileStore.cs          ← Generyczny store JSON-plikowy
  Events/
    EventBus.cs           ← Emiter zdarzeń z buforem i SSE replay
  Core/
    Runtime.cs            ← Kontener store'ów + helpery encji
    Log.cs                ← Kolorowy logger terminalowy z emisją zdarzeń
  Ai/
    AiClient.cs           ← Wrapper Responses API z retry (429/5xx)
  Agents/
    AgentDefinition.cs    ← Rejestr agentów: orchestrator, researcher, writer, email_writer
  Tools/
    ToolTypes.cs          ← Typy narzędzi i helpery argumentów
    ArtifactShared.cs     ← Wspólna logika artefaktów (I/O plików, normalizacja ścieżek)
    ToolRegistry.cs       ← Centralny rejestr definicji i handlerów 7 narzędzi
  Memory/
    Observer.cs           ← Ekstrakcja obserwacji z logów wykonania (LLM)
    Reflector.cs          ← Kompresja obserwacji (wielopoziomowa)
    MemoryProcessor.cs    ← Cykl observer/reflector przed każdym krokiem aktora
  Scheduler/
    Recovery.cs           ← Logika retry dla błędów przejściowych
    GraphQueries.cs       ← Zapytania grafu encji (zależności, gotowe zadania)
    ContextBuilder.cs     ← Budowanie promptu/inputu dla kroków aktora
    ActorRunner.cs        ← Pętla kroków aktora (LLM + narzędzia)
    SessionLoop.cs        ← Główna pętla przetwarzania sesji (round-robin)
  Server/
    DashboardServer.cs    ← HttpListener: pliki statyczne, SSE, API
  dashboard/
    index.html            ← Dashboard HTML
    styles.css            ← Dark-theme CSS
    app.js                ← Klient JS: SSE feed, graf Cytoscape, panele
  Program.cs              ← Punkt wejścia: bootstrap sesji, przetwarzanie, podsumowanie
```
