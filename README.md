# 4th-devs-dotnet48

Port ćwiczeń [`i-am-alice/4th-devs`](https://github.com/i-am-alice/4th-devs) na **C# / .NET Framework 4.8**.

## Wymagania

- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (runtime)
- [.NET SDK 6+](https://dotnet.microsoft.com/en-us/download) lub Visual Studio 2017+ (do budowania)
- Klucz API – OpenAI lub OpenRouter (patrz sekcja **Konfiguracja**)

## Konfiguracja

Każdy projekt wymaga własnego pliku `App.config` z kluczami API.
Szablon znajduje się w głównym katalogu repozytorium: [`App.example.config`](./App.example.config).

```powershell
# Skopiuj szablon do każdego projektu, który chcesz uruchomić
copy App.example.config src\01_01_interaction\App.config
copy App.example.config src\01_01_structured\App.config
copy App.example.config src\01_01_grounding\App.config
copy App.example.config src\01_02_tools\App.config
copy App.example.config src\01_02_tool_use\App.config
copy App.example.config src\01_05_confirmation\App.config
copy App.example.config src\01_05_agent\App.config
copy App.example.config src\02_01_agentic_rag\App.config
copy App.example.config src\02_02_chunking\App.config
copy App.example.config src\02_02_embedding\App.config
copy App.example.config src\02_02_hybrid_rag\App.config

# Następnie otwórz każdy App.config i uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY
```

> **⚠️ Uwaga:** `App.config` jest w `.gitignore` – nigdy nie commituj prawdziwych kluczy.

## Budowanie

```powershell
# Przywróć pakiety NuGet i zbuduj całe solution
dotnet restore 4th-devs-dotnet48.sln
dotnet build   4th-devs-dotnet48.sln
```

Lub otwórz `4th-devs-dotnet48.sln` w Visual Studio i wciśnij **Build → Build Solution**.

## Uruchamianie ćwiczeń

```powershell
dotnet run --project src\01_01_interaction\01_01_interaction.csproj
dotnet run --project src\01_01_structured\01_01_structured.csproj
dotnet run --project src\01_01_grounding\01_01_grounding.csproj
dotnet run --project src\01_02_tools\01_02_tools.csproj
dotnet run --project src\01_02_tool_use\01_02_tool_use.csproj
dotnet run --project src\01_05_confirmation\01_05_confirmation.csproj
dotnet run --project src\01_05_agent\01_05_agent.csproj
dotnet run --project src\02_01_agentic_rag\02_01_agentic_rag.csproj
dotnet run --project src\02_02_chunking\02_02_chunking.csproj
dotnet run --project src\02_02_embedding\02_02_embedding.csproj
dotnet run --project src\02_02_hybrid_rag\02_02_hybrid_rag.csproj
```

## Ćwiczenia

| Projekt | Oryginał JS | Opis |
|---------|------------|------|
| [`01_01_interaction`](src/01_01_interaction/) | `01_01_interaction` | Wieloturowa rozmowa z historią wiadomości |
| [`01_01_structured`](src/01_01_structured/) | `01_01_structured` | Ustrukturyzowane wyjście JSON ze schematem |
| [`01_01_grounding`](src/01_01_grounding/) | `01_01_grounding` | Fact-checked HTML z notatek Markdown |
| [`01_02_tools`](src/01_02_tools/) | `01_02_tools` | Function calling: get_weather + send_email |
| [`01_02_tool_use`](src/01_02_tool_use/) | `01_02_tool_use` | Sandboxed filesystem function calling |
| [`01_05_confirmation`](src/01_05_confirmation/) | `01_05_confirmation` | Agent plików i e-mail z potwierdzeniem (HITL) |
| [`01_05_agent`](src/01_05_agent/) | `01_05_agent` | Serwer HTTP REST z pętlą agentową i zarządzaniem sesjami |
| [`02_01_agentic_rag`](src/02_01_agentic_rag/) | `02_01_agentic_rag` | Agentic RAG z wieloetapowym wyszukiwaniem dokumentów |
| [`02_02_chunking`](src/02_02_chunking/) | `02_02_chunking` | Cztery strategie podziału tekstu: characters, separators, context, topics |
| [`02_02_embedding`](src/02_02_embedding/) | `02_02_embedding` | Interaktywne demo embeddingów z kolorową macierzą podobieństwa |
| [`02_02_hybrid_rag`](src/02_02_hybrid_rag/) | `02_02_hybrid_rag` | Hybrid RAG: SQLite FTS5 + cosine similarity + RRF |

Każdy projekt zawiera własny `README.md` z opisem i przykładem uruchomienia.

## Struktura repozytorium

```
4th-devs-dotnet48.sln       ← Solution Visual Studio / MSBuild
App.example.config          ← Szablon konfiguracji (skopiuj do App.config w każdym projekcie)
.gitignore
src/
  Common/                   ← Biblioteka współdzielona (AiConfig, ResponsesApiClient, modele)
  01_01_interaction/        ← Ćwiczenie: wieloturowa rozmowa
  01_01_structured/         ← Ćwiczenie: ustrukturyzowane wyjście
  01_01_grounding/          ← Ćwiczenie: gruntowanie faktów w HTML
  01_02_tools/              ← Ćwiczenie: function calling (narzędzia: pogoda, e-mail)
  01_02_tool_use/           ← Ćwiczenie: narzędzia systemu plików w piaskownicy
  01_05_confirmation/       ← Ćwiczenie: agent plików i e-mail z potwierdzeniem (HITL)
  01_05_agent/              ← Ćwiczenie: serwer HTTP REST z pętlą agentową
  02_01_agentic_rag/        ← Ćwiczenie: Agentic RAG z wieloetapowym wyszukiwaniem
  02_02_chunking/           ← Ćwiczenie: cztery strategie podziału tekstu
  02_02_embedding/          ← Ćwiczenie: interaktywne demo embeddingów
  02_02_hybrid_rag/         ← Ćwiczenie: Hybrid RAG (FTS5 + cosine similarity + RRF)
```

## Dodawanie nowych ćwiczeń

1. Utwórz nowy projekt Console App w `src/XX_YY_name/`:
   ```powershell
   dotnet new console -f net48 -o src/XX_YY_name -n XX_YY_name --langVersion 7.2
   ```
2. Dodaj referencję do `Common`:
   ```xml
   <ProjectReference Include="..\Common\Common.csproj" />
   ```
3. Skopiuj `App.example.config` → `App.config` i uzupełnij klucze.
4. Dodaj projekt do `4th-devs-dotnet48.sln` (Visual Studio: *Add Existing Project…*).
5. Utwórz `README.md` z opisem ćwiczenia.
6. Zaktualizuj tabelę w tym pliku.

## Biblioteki

| Pakiet | Wersja | Cel |
|--------|--------|-----|
| `Newtonsoft.Json` | 13.0.3 | Serializacja / deserializacja JSON |
| `System.Net.Http` | wbudowany w .NET 4.8 | Klient HTTP do Responses API |
| `System.Configuration` | wbudowany w .NET 4.8 | Odczyt App.config |
