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
copy App.example.config src\Lesson01_Interaction\App.config
copy App.example.config src\Lesson01_Structured\App.config
copy App.example.config src\Lesson01_Grounding\App.config
copy App.example.config src\Lesson02_Tools\App.config
copy App.example.config src\Lesson02_ToolUse\App.config
copy App.example.config src\Lesson05_Confirmation\App.config
copy App.example.config src\Lesson05_Agent\App.config
copy App.example.config src\Lesson06_AgenticRag\App.config
copy App.example.config src\Lesson07_Chunking\App.config
copy App.example.config src\Lesson07_Embedding\App.config
copy App.example.config src\Lesson07_HybridRag\App.config

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
dotnet run --project src\Lesson01_Interaction\Lesson01_Interaction.csproj
dotnet run --project src\Lesson01_Structured\Lesson01_Structured.csproj
dotnet run --project src\Lesson01_Grounding\Lesson01_Grounding.csproj
dotnet run --project src\Lesson02_Tools\Lesson02_Tools.csproj
dotnet run --project src\Lesson02_ToolUse\Lesson02_ToolUse.csproj
dotnet run --project src\Lesson05_Confirmation\Lesson05_Confirmation.csproj
dotnet run --project src\Lesson05_Agent\Lesson05_Agent.csproj
dotnet run --project src\Lesson06_AgenticRag\Lesson06_AgenticRag.csproj
dotnet run --project src\Lesson07_Chunking\Lesson07_Chunking.csproj
dotnet run --project src\Lesson07_Embedding\Lesson07_Embedding.csproj
dotnet run --project src\Lesson07_HybridRag\Lesson07_HybridRag.csproj
```

## Ćwiczenia

| Projekt | Oryginał JS | Opis |
|---------|------------|------|
| [`Lesson01_Interaction`](src/Lesson01_Interaction/) | `01_01_interaction` | Wieloturowa rozmowa z historią wiadomości |
| [`Lesson01_Structured`](src/Lesson01_Structured/) | `01_01_structured` | Ustrukturyzowane wyjście JSON ze schematem |
| [`Lesson01_Grounding`](src/Lesson01_Grounding/) | `01_01_grounding` | Fact-checked HTML z notatek Markdown |
| [`Lesson02_Tools`](src/Lesson02_Tools/) | `01_02_tools` | Function calling: get_weather + send_email |
| [`Lesson02_ToolUse`](src/Lesson02_ToolUse/) | `01_02_tool_use` | Sandboxed filesystem function calling |
| [`Lesson05_Confirmation`](src/Lesson05_Confirmation/) | `01_05_confirmation` | Agent plików i e-mail z potwierdzeniem (HITL) |
| [`Lesson05_Agent`](src/Lesson05_Agent/) | `01_05_agent` | Serwer HTTP REST z pętlą agentową i zarządzaniem sesjami |
| [`Lesson06_AgenticRag`](src/Lesson06_AgenticRag/) | `02_01_agentic_rag` | Agentic RAG z wieloetapowym wyszukiwaniem dokumentów |
| [`Lesson07_Chunking`](src/Lesson07_Chunking/) | `02_02_chunking` | Cztery strategie podziału tekstu: characters, separators, context, topics |
| [`Lesson07_Embedding`](src/Lesson07_Embedding/) | `02_02_embedding` | Interaktywne demo embeddingów z kolorową macierzą podobieństwa |
| [`Lesson07_HybridRag`](src/Lesson07_HybridRag/) | `02_02_hybrid_rag` | Hybrid RAG: SQLite FTS5 + cosine similarity + RRF |

Każdy projekt zawiera własny `README.md` z opisem i przykładem uruchomienia.

## Struktura repozytorium

```
4th-devs-dotnet48.sln       ← Solution Visual Studio / MSBuild
App.example.config          ← Szablon konfiguracji (skopiuj do App.config w każdym projekcie)
.gitignore
src/
  Common/                   ← Biblioteka współdzielona (AiConfig, ResponsesApiClient, modele)
  Lesson01_Interaction/     ← Ćwiczenie: wieloturowa rozmowa
  Lesson01_Structured/      ← Ćwiczenie: ustrukturyzowane wyjście
  Lesson01_Grounding/       ← Ćwiczenie: gruntowanie faktów w HTML
  Lesson02_Tools/           ← Ćwiczenie: function calling (narzędzia: pogoda, e-mail)
  Lesson02_ToolUse/         ← Ćwiczenie: narzędzia systemu plików w piaskownicy
  Lesson05_Confirmation/    ← Ćwiczenie: agent plików i e-mail z potwierdzeniem (HITL)
  Lesson05_Agent/           ← Ćwiczenie: serwer HTTP REST z pętlą agentową
  Lesson06_AgenticRag/      ← Ćwiczenie: Agentic RAG z wieloetapowym wyszukiwaniem
  Lesson07_Chunking/        ← Ćwiczenie: cztery strategie podziału tekstu
  Lesson07_Embedding/       ← Ćwiczenie: interaktywne demo embeddingów
  Lesson07_HybridRag/       ← Ćwiczenie: Hybrid RAG (FTS5 + cosine similarity + RRF)
```

## Dodawanie nowych ćwiczeń

1. Utwórz nowy projekt Console App w `src/LessonXX_Name/`:
   ```powershell
   dotnet new console -f net48 -o src/LessonXX_Name -n LessonXX_Name --langVersion 7.2
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
