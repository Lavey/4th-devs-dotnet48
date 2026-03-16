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
copy App.example.config src\Lesson03_McpCore\App.config
copy App.example.config src\Lesson03_McpNative\App.config
copy App.example.config src\Lesson03_McpTranslator\App.config
copy App.example.config src\Lesson03_UploadMcp\App.config
copy App.example.config src\Lesson04_Audio\App.config
copy App.example.config src\Lesson04_ImageEditing\App.config
copy App.example.config src\Lesson04_ImageGuidance\App.config
copy App.example.config src\Lesson04_ImageRecognition\App.config
copy App.example.config src\Lesson04_JsonImage\App.config
copy App.example.config src\Lesson04_Reports\App.config
copy App.example.config src\Lesson05_Confirmation\App.config
copy App.example.config src\Lesson05_Agent\App.config
copy App.example.config src\Lesson06_AgenticRag\App.config

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
dotnet run --project src\Lesson03_McpCore\Lesson03_McpCore.csproj
dotnet run --project src\Lesson03_McpNative\Lesson03_McpNative.csproj
dotnet run --project src\Lesson03_McpTranslator\Lesson03_McpTranslator.csproj
dotnet run --project src\Lesson03_UploadMcp\Lesson03_UploadMcp.csproj
dotnet run --project src\Lesson04_Audio\Lesson04_Audio.csproj
dotnet run --project src\Lesson04_ImageEditing\Lesson04_ImageEditing.csproj
dotnet run --project src\Lesson04_ImageGuidance\Lesson04_ImageGuidance.csproj
dotnet run --project src\Lesson04_ImageRecognition\Lesson04_ImageRecognition.csproj
dotnet run --project src\Lesson04_JsonImage\Lesson04_JsonImage.csproj
dotnet run --project src\Lesson04_Reports\Lesson04_Reports.csproj
dotnet run --project src\Lesson05_Confirmation\Lesson05_Confirmation.csproj
dotnet run --project src\Lesson05_Agent\Lesson05_Agent.csproj
dotnet run --project src\Lesson06_AgenticRag\Lesson06_AgenticRag.csproj
```

## Ćwiczenia

| Projekt | Oryginał JS | Opis |
|---------|------------|------|
| [`Lesson01_Interaction`](src/Lesson01_Interaction/) | `01_01_interaction` | Wieloturowa rozmowa z historią wiadomości |
| [`Lesson01_Structured`](src/Lesson01_Structured/) | `01_01_structured` | Ustrukturyzowane wyjście JSON ze schematem |
| [`Lesson01_Grounding`](src/Lesson01_Grounding/) | `01_01_grounding` | Fact-checked HTML z notatek Markdown |
| [`Lesson02_Tools`](src/Lesson02_Tools/) | `01_02_tools` | Function calling: get_weather + send_email |
| [`Lesson02_ToolUse`](src/Lesson02_ToolUse/) | `01_02_tool_use` | Sandboxed filesystem function calling |
| [`Lesson03_McpCore`](src/Lesson03_McpCore/) | `01_03_mcp_core` | Podstawowe możliwości MCP: narzędzia, zasoby, szablony promptów, elicitation, sampling |
| [`Lesson03_McpNative`](src/Lesson03_McpNative/) | `01_03_mcp_native` | Agent łączący narzędzia MCP i natywne w tej samej pętli |
| [`Lesson03_McpTranslator`](src/Lesson03_McpTranslator/) | `01_03_mcp_translator` | Agent tłumaczący pliki z workspace/translate/ do workspace/translated/ |
| [`Lesson03_UploadMcp`](src/Lesson03_UploadMcp/) | `01_03_upload_mcp` | Agent uploadujący pliki przez HTTP z rejestrem w uploaded.md |
| [`Lesson04_Audio`](src/Lesson04_Audio/) | `01_04_audio` | Transkrypcja audio (Whisper) i Text-to-Speech (TTS-1) |
| [`Lesson04_ImageEditing`](src/Lesson04_ImageEditing/) | `01_04_image_editing` | Generowanie i edytowanie obrazów z kontrolą jakości przez vision |
| [`Lesson04_ImageGuidance`](src/Lesson04_ImageGuidance/) | `01_04_image_guidance` | Generowanie obrazów z szablonu JSON i obrazu referencyjnego pozy |
| [`Lesson04_ImageRecognition`](src/Lesson04_ImageRecognition/) | `01_04_image_recognition` | Klasyfikacja obrazów przy użyciu modelu vision (gpt-4o) |
| [`Lesson04_JsonImage`](src/Lesson04_JsonImage/) | `01_04_json_image` | Token-efektywne generowanie obrazów z szablonów JSON |
| [`Lesson04_Reports`](src/Lesson04_Reports/) | `01_04_reports` | Agent analizy dokumentów i generowania raportów Markdown |
| [`Lesson05_Confirmation`](src/Lesson05_Confirmation/) | `01_05_confirmation` | Agent plików i e-mail z potwierdzeniem (HITL) |
| [`Lesson05_Agent`](src/Lesson05_Agent/) | `01_05_agent` | Serwer HTTP REST z pętlą agentową i zarządzaniem sesjami |
| [`Lesson06_AgenticRag`](src/Lesson06_AgenticRag/) | `02_01_agentic_rag` | Agentic RAG z wieloetapowym wyszukiwaniem po lokalnych dokumentach |

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
  Lesson03_McpCore/         ← Ćwiczenie: podstawowe możliwości MCP
  Lesson03_McpNative/       ← Ćwiczenie: narzędzia MCP i natywne w jednej pętli
  Lesson03_McpTranslator/   ← Ćwiczenie: agent tłumaczący pliki przez MCP
  Lesson03_UploadMcp/       ← Ćwiczenie: agent uploadujący pliki przez MCP
  Lesson04_Audio/           ← Ćwiczenie: transkrypcja audio i TTS
  Lesson04_ImageEditing/    ← Ćwiczenie: generowanie i edytowanie obrazów
  Lesson04_ImageGuidance/   ← Ćwiczenie: generowanie obrazów z szablonu i obrazu referencyjnego
  Lesson04_ImageRecognition/ ← Ćwiczenie: klasyfikacja obrazów przez model vision
  Lesson04_JsonImage/       ← Ćwiczenie: generowanie obrazów z szablonów JSON
  Lesson04_Reports/         ← Ćwiczenie: agent analizy dokumentów i raportów
  Lesson05_Confirmation/    ← Ćwiczenie: agent plików i e-mail z potwierdzeniem (HITL)
  Lesson05_Agent/           ← Ćwiczenie: serwer HTTP REST z pętlą agentową
  Lesson06_AgenticRag/      ← Ćwiczenie: Agentic RAG z wieloetapowym wyszukiwaniem
```

## Dodawanie nowych ćwiczeń

1. Utwórz nowy projekt Console App w `src/LessonXX_Name/`:
   ```powershell
   dotnet new console -f net48 -o src/LessonXX_Name -n LessonXX_Name --langVersion 7.3
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
