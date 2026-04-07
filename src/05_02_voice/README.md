# Lesson 05 — Voice Agent

## Cel ćwiczenia
**Agent głosowy** z integracją LiveKit, narzędziami MCP i obsługą wielu
dostawców głosu (Gemini Realtime, OpenAI Realtime, ElevenLabs TTS).

## Oryginał JS
`i-am-alice/4th-devs` → `05_02_voice` (LiveKit Agents + Hono + Bun)

## Jak działa
1. Serwer tokenów (`HttpListener`) nasłuchuje na porcie **3310**
2. Dashboard (statyczny HTML z LiveKit Client SDK) ładowany z katalogu `dashboard/`
3. `GET /api/config` → zwraca wykryty tryb głosowy (Gemini / ElevenLabs / OpenAI)
4. `GET /api/token` → generuje JWT token LiveKit + dane pokoju
5. Dashboard łączy się z serwerem LiveKit, publikuje mikrofon i wyświetla wizualizację
6. Tryb głosowy jest automatycznie wykrywany na podstawie dostępnych kluczy API

## Tryby głosowe

| Tryb         | Wymagane klucze                             | Opis                                        |
|--------------|---------------------------------------------|---------------------------------------------|
| **Gemini**   | `GOOGLE_API_KEY` lub `GEMINI_API_KEY`       | Google Gemini multimodal realtime            |
| **ElevenLabs** | `ELEVEN_API_KEY` + `OPENAI_API_KEY`       | ElevenLabs TTS z backendem OpenAI            |
| **OpenAI**   | `OPENAI_API_KEY`                            | OpenAI realtime voice mode                   |

## Różnice względem oryginału
- Backend: C# `.NET 4.8` + `HttpListener` zamiast Bun + Hono + LiveKit Agents SDK
- Brak natywnego agenta LiveKit — serwer generuje tokeny i serwuje dashboard
- Agent głosowy wymaga osobnego serwera LiveKit (self-hosted lub LiveKit Cloud)
- JWT tokeny generowane za pomocą `System.IdentityModel.Tokens.Jwt`
- Frontend: oryginalny HTML z LiveKit Client SDK z CDN (bez zmian)
- Konfiguracja MCP ładowana z `.mcp.json` (jeśli istnieje)

## Wymagania
- .NET Framework 4.8
- Serwer LiveKit (lokalny lub LiveKit Cloud)
- Co najmniej jeden klucz API głosowego (patrz tabela trybów)

## Uruchomienie

### Konfiguracja LiveKit
```bash
# Opcja 1: LiveKit Cloud (https://cloud.livekit.io/)
# Opcja 2: Lokalny serwer LiveKit
docker run --rm -p 7880:7880 -p 7881:7881 -p 7882:7882/udp \
  livekit/livekit-server --dev --bind 0.0.0.0
```

### Konfiguracja projektu
```powershell
cd src\05_02_voice
copy App.config.example App.config
# Uzupełnij klucze API w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src\05_02_voice\05_02_voice.csproj
```

Serwer uruchomi się na `http://localhost:3310` i automatycznie otworzy przeglądarkę.

### Oczekiwany wynik
```
[05_02_voice] Voice mode: Gemini Realtime (gemini)
[05_02_voice] Token server at http://localhost:3310 — Ctrl+C to stop
[05_02_voice] Note: LiveKit agent requires a running LiveKit server
```

## Endpointy API

| Metoda | Ścieżka        | Opis                                          |
|--------|----------------|-----------------------------------------------|
| GET    | `/api/config`  | Status trybu głosowego                        |
| GET    | `/api/token`   | Token JWT LiveKit + dane pokoju               |
| GET    | `/*`           | Statyczne pliki dashboardu                    |
