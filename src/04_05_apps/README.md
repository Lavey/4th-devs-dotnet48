# 04_05_apps – Marketing Ops Agent

Port ćwiczenia [`i-am-alice/4th-devs/04_05_apps`](https://github.com/i-am-alice/4th-devs/tree/main/04_05_apps) na C# / .NET Framework 4.8.

## Opis

Agent marketingowy SaaS z przeglądarką UI i narzędziami do zarządzania:
- **Todos** — tablica zadań w Markdown (`workspace/todos.md`)
- **Campaigns** — przegląd kampanii newsletterowych (JSON)
- **Sales** — analityka sprzedaży z filtrowaniem po dacie i produkcie
- **Coupons** — tworzenie, listowanie i deaktywacja kuponów
- **Products** — katalog produktów z edycją

Agent używa Responses API (tool-calling loop, max 8 rund). Bez klucza API działa w trybie fallback (pattern matching).

## Uruchomienie

```powershell
copy App.example.config src\04_05_apps\App.config
# Uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY

dotnet run --project src\04_05_apps\04_05_apps.csproj
```

UI otwiera się w przeglądarce na `http://localhost:4500/`. CLI również dostępne w terminalu.

## Architektura

| Warstwa | Pliki |
|---------|-------|
| Program.cs | Punkt wejścia — start serwera + CLI |
| Agent/AgentRunner.cs | Pętla tool-calling (Responses API, fallback) |
| Core/ToolRegistry.cs | Rejestr 20+ narzędzi z handlerami |
| Core/AppServer.cs | HttpListener: API + inline HTML/JS UI |
| Store/TodoStore.cs | Markdown-based todo management |
| Store/StripeStore.cs | JSON-based products/coupons/sales |
| Store/NewsletterStore.cs | Campaign data (read-only + comparison) |

## API

| Endpoint | Metoda | Opis |
|----------|--------|------|
| `/api/bootstrap` | GET | Podsumowania + sugestie czatu |
| `/api/chat` | POST | `{message, context}` → agent turn |
| `/api/mcp/tools/list` | POST | Lista definicji narzędzi |
| `/api/mcp/tools/call` | POST | `{name, arguments}` → wywołanie narzędzia |
