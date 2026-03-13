# Lesson 05 – Agent API Server

## Cel ćwiczenia

Lekki **serwer HTTP REST** uruchamiający agenty AI na żądanie.

Klient wysyła zapytanie POST; serwer uruchamia pętlę agentową (wywołania narzędzi,
historia sesji) i zwraca wynik. Obsługuje uwierzytelnianie Bearer token,
persystencję sesji (w pamięci) i szablony agentów z plików `.agent.md`.

## Oryginał JS

`i-am-alice/4th-devs` → `01_05_agent/`

## Jak działa

1. Serwer nasłuchuje na `http://127.0.0.1:3000/` (konfigurowalny HOST/PORT).
2. Wszystkie endpointy poza `/health` wymagają nagłówka `Authorization: Bearer <token>`.
3. `POST /api/chat/completions` – uruchamia pętlę agentową:
   - Ładuje szablon agenta z `workspace/agents/<name>.agent.md` (jeśli podano `agent`).
   - Wykonuje pętlę narzędziową (kalkulator, operacje na plikach).
   - Zapisuje historię w sesji dla wieloturowych rozmów.
4. Sesja jest identyfikowana przez `sessionId`; jeśli go nie podasz, zostanie wygenerowany.

## Narzędzia agenta

| Narzędzie    | Opis                                              |
|--------------|---------------------------------------------------|
| `calculator` | Oblicza wyrażenie matematyczne                    |
| `list_files` | Listuje pliki w `workspace/`                      |
| `read_file`  | Czyta plik z `workspace/`                         |
| `write_file` | Zapisuje plik do `workspace/`                     |

## Uruchomienie

### Konfiguracja

```powershell
copy App.config.example App.config
# Uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY
# Opcjonalnie zmień AUTH_TOKEN, PORT, HOST
```

### Budowanie i uruchomienie

```powershell
dotnet run --project src/Lesson05_Agent/Lesson05_Agent.csproj
```

### Przykłady użycia (curl)

```bash
# Health check
curl -s http://127.0.0.1:3000/health | jq

# Wyślij pytanie do agenta alice
curl -s http://127.0.0.1:3000/api/chat/completions \
  -H "Authorization: Bearer 0f47acce-3aa7-4b58-9389-21b2940ecc70" \
  -H "Content-Type: application/json" \
  -d '{"agent":"alice","input":"What is 42 * 17?"}' | jq

# Wieloturowa rozmowa – zapisz sessionId
SESSION=$(curl -s http://127.0.0.1:3000/api/chat/completions \
  -H "Authorization: Bearer 0f47acce-3aa7-4b58-9389-21b2940ecc70" \
  -H "Content-Type: application/json" \
  -d '{"agent":"alice","input":"Remember: my name is Adam"}' | jq -r '.data.sessionId')

curl -s http://127.0.0.1:3000/api/chat/completions \
  -H "Authorization: Bearer 0f47acce-3aa7-4b58-9389-21b2940ecc70" \
  -H "Content-Type: application/json" \
  -d "{\"agent\":\"alice\",\"sessionId\":\"$SESSION\",\"input\":\"What is my name?\"}" | jq

# Lista serwerów MCP
curl -s http://127.0.0.1:3000/api/mcp/servers \
  -H "Authorization: Bearer 0f47acce-3aa7-4b58-9389-21b2940ecc70" | jq
```

## Endpointy API

| Metoda | Ścieżka                             | Opis                       |
|--------|-------------------------------------|----------------------------|
| GET    | `/health`                           | Sprawdzenie stanu serwera  |
| GET    | `/api/mcp/servers`                  | Lista serwerów MCP         |
| POST   | `/api/chat/completions`             | Uruchomienie agenta        |
| GET    | `/api/chat/agents/:agentId`         | Status przebiegu agenta    |

### Schemat żądania POST /api/chat/completions

```json
{
  "agent": "alice",
  "input": "What is 42 * 17?",
  "model": "openai:gpt-4.1-mini",
  "sessionId": "optional-session-id",
  "instructions": "Override system prompt"
}
```

## Zmienne środowiskowe (App.config)

| Klucz             | Domyślnie                             | Opis                              |
|-------------------|---------------------------------------|-----------------------------------|
| `AUTH_TOKEN`      | `0f47acce-3aa7-4b58-9389-21b2940ecc70`| Bearer token uwierzytelniający    |
| `HOST`            | `127.0.0.1`                           | Adres serwera                     |
| `PORT`            | `3000`                                | Port serwera                      |
| `WORKSPACE_PATH`  | `workspace/` obok exe                 | Katalog z szablonami agentów      |
| `AGENT_MAX_TURNS` | `10`                                  | Maksymalna liczba kroków agenta   |
