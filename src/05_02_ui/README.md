# Lesson 05 — Streaming Chat UI

## Cel ćwiczenia
Interaktywny **dashboard czatowy** z backendem agentowym i strumieniowaniem SSE
(Server-Sent Events). Obsługuje tryb **mock** (gotowe scenariusze) oraz **live**
(streaming z OpenAI Responses API).

## Oryginał JS
`i-am-alice/4th-devs` → `05_02_ui` (Svelte 5 + Bun)

## Jak działa
1. Serwer HTTP (`HttpListener`) nasłuchuje na porcie **3300**
2. Dashboard (statyczny HTML + vanilla JS) ładowany z katalogu `dashboard/`
3. Wiadomość użytkownika → POST `/api/chat` → strumień SSE zdarzeń
4. Zdarzenia: `text_delta`, `thinking_start/delta/end`, `tool_call`, `tool_result`, `artifact`, `error`, `complete`
5. Tryb **mock** — cztery pre-skryptowane scenariusze (sales, email, artifact, research)
6. Tryb **live** — pętla agentowa z Responses API (streaming, tool calls, do 6 kroków)

## Narzędzia agenta

| Narzędzie                 | Opis                                                    |
|---------------------------|---------------------------------------------------------|
| `get_sales_report`        | Pobiera dane sprzedażowe i przychody                   |
| `render_chart`            | Generuje wizualizację wykresu                          |
| `lookup_contact_context`  | Wyszukuje kontekst kontaktu przed wysłaniem e-maila    |
| `send_email`              | Wysyła e-mail do wskazanego odbiorcy                   |
| `create_artifact`         | Tworzy artefakt (markdown, JSON, tekst, plik)          |
| `search_notes`            | Przeszukuje notatki użytkownika                        |

## Różnice względem oryginału
- Backend: C# `.NET 4.8` + `HttpListener` zamiast Bun + Hono
- Frontend: vanilla JS + inline CSS zamiast Svelte 5
- Brak `IAsyncEnumerable` (C# 7.3) — callbacki zamiast async iteratorów
- Dane narzędzi to mocki (brak prawdziwych baz danych)
- Artefakty zapisywane do katalogu `.data/`

## Wymagania
- Tryb **mock** — bez klucza API
- Tryb **live** — `OPENAI_API_KEY` lub `OPENROUTER_API_KEY`

## Uruchomienie

### Konfiguracja
```powershell
copy App.config.example App.config
# opcjonalne (dla trybu live): OPENAI_API_KEY lub OPENROUTER_API_KEY
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src\05_02_ui\05_02_ui.csproj
```

Serwer uruchomi się na `http://localhost:3300` i automatycznie otworzy przeglądarkę.

### Oczekiwany wynik
```
[05_02_ui] server at http://localhost:3300 — Ctrl+C to stop
```

Dashboard wyświetli interfejs czatowy. W trybie mock zobaczysz gotowe przykłady
konwersacji z narzędziami. W trybie live agent będzie korzystał z Responses API
i narzędzi w czasie rzeczywistym.

## Endpointy API

| Metoda | Ścieżka                | Opis                                    |
|--------|-------------------------|-----------------------------------------|
| GET    | `/api/health`           | Status serwera                          |
| GET    | `/api/conversation`     | Snapshot konwersacji (`?mode=&history=`) |
| POST   | `/api/reset`            | Reset konwersacji                       |
| POST   | `/api/chat`             | Strumień SSE z odpowiedzią agenta       |
| GET    | `/api/artifacts/*`      | Serwowanie plików artefaktów            |
| GET    | `/*`                    | Statyczne pliki dashboardu              |
