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
copy App.example.config src\01_03_mcp_core\App.config
copy App.example.config src\01_03_mcp_native\App.config
copy App.example.config src\01_03_mcp_translator\App.config
copy App.example.config src\01_03_upload_mcp\App.config
copy App.example.config src\01_04_audio\App.config
copy App.example.config src\01_04_image_editing\App.config
copy App.example.config src\01_04_image_guidance\App.config
copy App.example.config src\01_04_image_recognition\App.config
copy App.example.config src\01_04_json_image\App.config
copy App.example.config src\01_04_reports\App.config
copy App.example.config src\01_04_video\App.config
copy App.example.config src\01_04_video_generation\App.config
copy App.example.config src\01_05_confirmation\App.config
copy App.example.config src\01_05_agent\App.config
copy App.example.config src\02_01_agentic_rag\App.config
copy App.example.config src\02_02_chunking\App.config
copy App.example.config src\02_02_embedding\App.config
copy App.example.config src\02_02_hybrid_rag\App.config
copy App.example.config src\02_03_graph_agents\App.config
copy App.example.config src\02_04_ops\App.config
copy App.example.config src\02_05_agent\App.config
copy App.example.config src\02_05_sandbox\App.config
copy App.example.config src\03_01_observability\App.config
copy App.example.config src\03_01_evals\App.config
copy App.example.config src\03_02_events\App.config
copy App.example.config src\03_02_code\App.config
copy App.example.config src\03_02_email\App.config
copy App.example.config src\03_03_calendar\App.config
copy App.example.config src\03_03_browser\App.config
copy App.example.config src\03_03_language\App.config
copy src\03_04_gmail\App.config.example src\03_04_gmail\App.config
copy App.example.config src\03_05_awareness\App.config
copy App.example.config src\03_05_apps\App.config
copy App.example.config src\03_05_artifacts\App.config
copy App.example.config src\03_05_render\App.config
copy App.example.config src\04_01_garden\App.config
copy App.example.config src\04_04_system\App.config
copy App.example.config src\04_05_review\App.config
copy App.example.config src\04_05_apps\App.config
copy App.example.config src\05_01_agent_graph\App.config

# Projekty wymagające dodatkowych kluczy API:
# 01_04_video i 01_04_video_generation: ustaw GEMINI_API_KEY
# 01_04_video_generation: ustaw też REPLICATE_API_TOKEN (Kling video)
# 03_04_gmail: ustaw GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET (patrz App.config.example w projekcie)
# Dla tych projektów użyj ich własnego App.config.example jako szablonu

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
dotnet run --project src\01_03_mcp_core\01_03_mcp_core.csproj
dotnet run --project src\01_03_mcp_native\01_03_mcp_native.csproj
dotnet run --project src\01_03_mcp_translator\01_03_mcp_translator.csproj
dotnet run --project src\01_03_upload_mcp\01_03_upload_mcp.csproj
dotnet run --project src\01_04_audio\01_04_audio.csproj
dotnet run --project src\01_04_image_editing\01_04_image_editing.csproj
dotnet run --project src\01_04_image_guidance\01_04_image_guidance.csproj
dotnet run --project src\01_04_image_recognition\01_04_image_recognition.csproj
dotnet run --project src\01_04_json_image\01_04_json_image.csproj
dotnet run --project src\01_04_reports\01_04_reports.csproj
dotnet run --project src\01_04_video\01_04_video.csproj
dotnet run --project src\01_04_video_generation\01_04_video_generation.csproj
dotnet run --project src\01_05_confirmation\01_05_confirmation.csproj
dotnet run --project src\01_05_agent\01_05_agent.csproj
dotnet run --project src\02_01_agentic_rag\02_01_agentic_rag.csproj
dotnet run --project src\02_02_chunking\02_02_chunking.csproj
dotnet run --project src\02_02_embedding\02_02_embedding.csproj
dotnet run --project src\02_02_hybrid_rag\02_02_hybrid_rag.csproj
dotnet run --project src\02_03_graph_agents\02_03_graph_agents.csproj
dotnet run --project src\02_04_ops\02_04_ops.csproj
dotnet run --project src\02_05_agent\02_05_agent.csproj
dotnet run --project src\02_05_sandbox\02_05_sandbox.csproj
dotnet run --project src\03_01_observability\03_01_observability.csproj
dotnet run --project src\03_01_evals\03_01_evals.csproj
dotnet run --project src\03_02_events\03_02_events.csproj
dotnet run --project src\03_02_code\03_02_code.csproj
dotnet run --project src\03_02_email\03_02_email.csproj
dotnet run --project src\03_03_calendar\03_03_calendar.csproj
dotnet run --project src\03_03_browser\03_03_browser.csproj
dotnet run --project src\03_03_language\03_03_language.csproj

# 03_04_gmail — najpierw autoryzacja, potem czat:
dotnet run --project src\03_04_gmail\03_04_gmail.csproj -- auth
dotnet run --project src\03_04_gmail\03_04_gmail.csproj

# 03_05_awareness — agent świadomości (wypełnij workspace/profile przed uruchomieniem):
dotnet run --project src\03_05_awareness\03_05_awareness.csproj

# 03_05_apps — agent menedżera list (otwiera UI w przeglądarce na porcie 3500):
dotnet run --project src\03_05_apps\03_05_apps.csproj

# 03_05_artifacts — agent artefaktów HTML (live preview na porcie 3501):
dotnet run --project src\03_05_artifacts\03_05_artifacts.csproj

# 03_05_render — agent renderowania dashboardów (live preview na porcie 3502):
dotnet run --project src\03_05_render\03_05_render.csproj

# 04_01_garden — cyfrowy ogród: agent z bazą wiedzy Markdown, narzędziami i umiejętnościami:
dotnet run --project src\04_01_garden\04_01_garden.csproj

# 04_04_system — multi-agent system z bazą wiedzy Markdown sterującą zachowaniem agentów:
dotnet run --project src\04_04_system\04_04_system.csproj

# 04_04_system — workflow daily-news (research → assemble → deliver):
dotnet run --project src\04_04_system\04_04_system.csproj -- daily-news

# 04_04_system — przykładowe zapytania (wszystkie 7 lub jedno):
dotnet run --project src\04_04_system\04_04_system.csproj -- examples
dotnet run --project src\04_04_system\04_04_system.csproj -- examples 3

# 04_05_review — laboratorium recenzji Markdown (UI na porcie 4405):
dotnet run --project src\04_05_review\04_05_review.csproj

# 04_05_apps — agent marketingowy z narzędziami (UI na porcie 4500):
dotnet run --project src\04_05_apps\04_05_apps.csproj

# 05_01_agent_graph — multi-agent graph: orkiestrator + specjaliści, dashboard na porcie 3300:
dotnet run --project src\05_01_agent_graph\05_01_agent_graph.csproj
dotnet run --project src\05_01_agent_graph\05_01_agent_graph.csproj -- "Write a blog post about AI agents"
```

## Ćwiczenia

| Projekt | Oryginał JS | Opis |
|---------|------------|------|
| [`01_01_interaction`](src/01_01_interaction/) | `01_01_interaction` | Wieloturowa rozmowa z historią wiadomości |
| [`01_01_structured`](src/01_01_structured/) | `01_01_structured` | Ustrukturyzowane wyjście JSON ze schematem |
| [`01_01_grounding`](src/01_01_grounding/) | `01_01_grounding` | Fact-checked HTML z notatek Markdown |
| [`01_02_tools`](src/01_02_tools/) | `01_02_tools` | Function calling: get_weather + send_email |
| [`01_02_tool_use`](src/01_02_tool_use/) | `01_02_tool_use` | Sandboxed filesystem function calling |
| [`01_03_mcp_core`](src/01_03_mcp_core/) | `01_03_mcp_core` | Demonstracja MCP (narzędzia, zasoby, szablony promptów) — in-process server |
| [`01_03_mcp_native`](src/01_03_mcp_native/) | `01_03_mcp_native` | Agent łączący narzędzia MCP i natywne w jednej pętli tool-call |
| [`01_03_mcp_translator`](src/01_03_mcp_translator/) | `01_03_mcp_translator` | Agent obserwujący folder, tłumaczący pliki tekstowe na angielski |
| [`01_03_upload_mcp`](src/01_03_upload_mcp/) | `01_03_upload_mcp` | Agent uploadujący pliki z workspace, prowadzący rejestr w uploaded.md |
| [`01_04_audio`](src/01_04_audio/) | `01_04_audio` | Transkrypcja audio (Whisper) i generowanie mowy (TTS) przez OpenAI API |
| [`01_04_image_editing`](src/01_04_image_editing/) | `01_04_image_editing` | Agent generowania i edytowania obrazów z recenzją przez vision AI |
| [`01_04_image_guidance`](src/01_04_image_guidance/) | `01_04_image_guidance` | Generowanie obrazów oparte na szablonie JSON i obrazie referencyjnym pozy |
| [`01_04_image_recognition`](src/01_04_image_recognition/) | `01_04_image_recognition` | Agent klasyfikujący obrazy przy użyciu vision AI i profili kategorii |
| [`01_04_json_image`](src/01_04_json_image/) | `01_04_json_image` | Token-efektywne generowanie obrazów z szablonów JSON — reprodukowalne prompty |
| [`01_04_reports`](src/01_04_reports/) | `01_04_reports` | Agent do analizy dokumentów i generowania ustrukturyzowanych raportów Markdown |
| [`01_04_video`](src/01_04_video/) | `01_04_video` | Agent analizy wideo: transkrypcja, ekstrakcja scen i odpowiedzi na pytania przez Gemini API (YouTube/lokalne) |
| [`01_04_video_generation`](src/01_04_video_generation/) | `01_04_video_generation` | Agent generowania wideo: klatki startowa/końcowa przez Gemini + animacja przez Replicate Kling |
| [`01_05_confirmation`](src/01_05_confirmation/) | `01_05_confirmation` | Agent plików i e-mail z potwierdzeniem (HITL) |
| [`01_05_agent`](src/01_05_agent/) | `01_05_agent` | Serwer HTTP REST z pętlą agentową i zarządzaniem sesjami |
| [`02_01_agentic_rag`](src/02_01_agentic_rag/) | `02_01_agentic_rag` | Agentic RAG z wieloetapowym wyszukiwaniem dokumentów |
| [`02_02_chunking`](src/02_02_chunking/) | `02_02_chunking` | Cztery strategie podziału tekstu: characters, separators, context, topics |
| [`02_02_embedding`](src/02_02_embedding/) | `02_02_embedding` | Interaktywne demo embeddingów z kolorową macierzą podobieństwa |
| [`02_02_hybrid_rag`](src/02_02_hybrid_rag/) | `02_02_hybrid_rag` | Hybrid RAG: SQLite FTS5 + cosine similarity + RRF |
| [`02_03_graph_agents`](src/02_03_graph_agents/) | `02_03_graph_agents` | Agent RAG z grafem wiedzy Neo4j — hybrid search i eksploracja entności |
| [`02_04_ops`](src/02_04_ops/) | `02_04_ops` | Multi-agent Daily Ops: orkiestrator deleguje do agentów mail/calendar/tasks/notes |
| [`02_05_agent`](src/02_05_agent/) | `02_05_agent` | Agent z pamięcią obserwacyjną (Observer/Reflector) — serwer HTTP REST z zarządzaniem sesjami |
| [`02_05_sandbox`](src/02_05_sandbox/) | `02_05_sandbox` | Agent MCP sandbox: dynamiczne odkrywanie narzędzi i wykonywanie JS w Jint |
| [`03_01_observability`](src/03_01_observability/) | `03_01_observability` | Serwer HTTP z agentem obsługujący obserwowalność i integrację Langfuse |
| [`03_01_evals`](src/03_01_evals/) | `03_01_evals` | Serwer HTTP z agentem wspierającym eksperymenty oceny vs. syntetyczne datasety |
| [`03_02_events`](src/03_02_events/) | `03_02_events` | Multi-agent event architecture — heartbeat loop z workflow coordination |
| [`03_02_code`](src/03_02_code/) | `03_02_code` | Agent uruchamiający kod TypeScript w sandboxie Deno z dostępem do narzędzi MCP |
| [`03_02_email`](src/03_02_email/) | `03_02_email` | Agent dwufazowy: triaging i isolation mailów z bazą wiedzy per-odpowiedź |
| [`03_03_calendar`](src/03_03_calendar/) | `03_03_calendar` | Agent kalendarza dwufazowy: dodawanie zdarzeń + powiadomienia o nadchodzących eventach |
| [`03_03_browser`](src/03_03_browser/) | `03_03_browser` | Agent przeglądarkowy: interaktywny czat z Selenium WebDriver, obsługa Goodreads |
| [`03_03_language`](src/03_03_language/) | `03_03_language` | Coach języka angielskiego: ASR + analiza wymowy + TTS feedback (Gemini API) |
| [`03_04_gmail`](src/03_04_gmail/) | `03_04_gmail` | Agent Gmail z OAuth 2.0: wyszukiwanie, czytanie, wysyłanie, modyfikacja maili i pobieranie załączników |
| [`03_05_awareness`](src/03_05_awareness/) | `03_05_awareness` | Agent świadomości z kontekstem temporalnym, narzędziami `think`/`recall` i pamięcią w plikach workspace |
| [`03_05_apps`](src/03_05_apps/) | `03_05_apps` | Agent menedżera list (todo/shopping) w Markdown z przeglądarką UI serwowaną przez HttpListener |
| [`03_05_artifacts`](src/03_05_artifacts/) | `03_05_artifacts` | Agent generowania artefaktów HTML z zestawami zdolności (CDN) i podglądem na żywo w przeglądarce |
| [`03_05_render`](src/03_05_render/) | `03_05_render` | Agent renderowania dashboardów: specyfikacje JSON → HTML przez deterministyczny katalog komponentów |
| [`04_01_garden`](src/04_01_garden/) | `04_01_garden` | Cyfrowy ogród: agent z bazą wiedzy Markdown, narzędziami `terminal`/`code_mode`/`git_push`, systemem umiejętności i workflow |
| [`04_04_system`](src/04_04_system/) | `04_04_system` | Multi-agent system: baza wiedzy Markdown steruje agentami, delegacja zadań, workflow daily-news (research → assemble → deliver) |
| [`04_05_review`](src/04_05_review/) | `04_05_review` | Laboratorium recenzji Markdown: agent z komentarzami inline, tryby paragraph/at_once, accept/reject/revert sugestii |
| [`04_05_apps`](src/04_05_apps/) | `04_05_apps` | Agent marketingowy SaaS: zarządzanie todos, kampaniami, sprzedażą, kuponami i produktami z UI w przeglądarce |
| [`05_01_agent_graph`](src/05_01_agent_graph/) | `05_01_agent_graph` | Multi-agent graph: orkiestrator + specjaliści (researcher, writer, email_writer), pamięć obserwacyjna, scheduler z retry, dashboard na porcie 3300 |

Każdy projekt zawiera własny `README.md` z opisem i przykładem uruchomienia.

## Struktura repozytorium

```
4th-devs-dotnet48.sln       ← Solution Visual Studio / MSBuild
App.example.config          ← Szablon konfiguracji (skopiuj do App.config w każdym projekcie)
.gitignore
src/
  Common/                   ← Biblioteka współdzielona (AiConfig, ResponsesApiClient, modele)
  MCP/                      ← Unified MCP server — tryb stdio (files) lub HTTP (uploadthing)
  01_01_interaction/        ← Ćwiczenie: wieloturowa rozmowa
  01_01_structured/         ← Ćwiczenie: ustrukturyzowane wyjście
  01_01_grounding/          ← Ćwiczenie: gruntowanie faktów w HTML
  01_02_tools/              ← Ćwiczenie: function calling (narzędzia: pogoda, e-mail)
  01_02_tool_use/           ← Ćwiczenie: narzędzia systemu plików w piaskownicy
  01_03_mcp_core/           ← Ćwiczenie: demonstracja MCP (narzędzia, zasoby, prompty)
  01_03_mcp_native/         ← Ćwiczenie: agent łączący MCP i natywne narzędzia
  01_03_mcp_translator/     ← Ćwiczenie: agent tłumaczący pliki z obserwacją folderu
  01_03_upload_mcp/         ← Ćwiczenie: agent uploadujący pliki z rejestrem
  01_04_audio/              ← Ćwiczenie: transkrypcja audio (Whisper) i TTS
  01_04_image_editing/      ← Ćwiczenie: generowanie i edycja obrazów z vision AI
  01_04_image_guidance/     ← Ćwiczenie: generowanie obrazów z szablonu i pozy referencyjnej
  01_04_image_recognition/  ← Ćwiczenie: klasyfikacja obrazów przez vision AI
  01_04_json_image/         ← Ćwiczenie: generowanie obrazów z szablonów JSON
  01_04_reports/            ← Ćwiczenie: analiza dokumentów i raporty Markdown
  01_04_video/              ← Ćwiczenie: agent analizy wideo (Gemini — YouTube i pliki lokalne)
  01_04_video_generation/   ← Ćwiczenie: generowanie wideo (klatki Gemini + animacja Replicate Kling)
  01_05_confirmation/       ← Ćwiczenie: agent plików i e-mail z potwierdzeniem (HITL)
  01_05_agent/              ← Ćwiczenie: serwer HTTP REST z pętlą agentową
  02_01_agentic_rag/        ← Ćwiczenie: Agentic RAG z wieloetapowym wyszukiwaniem
  02_02_chunking/           ← Ćwiczenie: cztery strategie podziału tekstu
  02_02_embedding/          ← Ćwiczenie: interaktywne demo embeddingów
  02_02_hybrid_rag/         ← Ćwiczenie: Hybrid RAG (FTS5 + cosine similarity + RRF)
  02_03_graph_agents/       ← Ćwiczenie: agent RAG z grafem wiedzy Neo4j
  02_04_ops/                ← Ćwiczenie: multi-agent Daily Ops (orkiestrator + agenci specjaliści)
  02_05_agent/              ← Ćwiczenie: agent z pamięcią obserwacyjną (Observer/Reflector, serwer HTTP)
  02_05_sandbox/            ← Ćwiczenie: agent MCP sandbox (odkrywanie narzędzi + JS w Jint)
  03_01_observability/      ← Ćwiczenie: obserwowalność agentów z integracją Langfuse
  03_01_evals/              ← Ćwiczenie: eksperymenty oceny vs. syntetyczne datasety
  03_02_events/             ← Ćwiczenie: multi-agent event architecture z heartbeat loop
  03_02_code/               ← Ćwiczenie: agent uruchamiający kod w sandboxie Deno + MCP
  03_02_email/              ← Ćwiczenie: agent dwufazowy triaging i isolation mailów
  03_03_calendar/           ← Ćwiczenie: agent kalendarza (dodawanie zdarzeń + powiadomienia)
  03_03_browser/            ← Ćwiczenie: agent przeglądarkowy (Selenium WebDriver + Goodreads)
  03_03_language/           ← Ćwiczenie: coach języka angielskiego (Gemini ASR + TTS)
  03_04_gmail/              ← Ćwiczenie: agent Gmail z OAuth 2.0 (wyszukiwanie, czytanie, wysyłanie, modyfikacja)
  03_05_awareness/          ← Ćwiczenie: agent świadomości (kontekst temporalny, think/recall, pamięć workspace)
  03_05_apps/               ← Ćwiczenie: agent menedżera list (todo/shopping Markdown + UI HttpListener)
  03_05_artifacts/          ← Ćwiczenie: agent artefaktów HTML (zestawy zdolności CDN + live preview)
  03_05_render/             ← Ćwiczenie: agent renderowania dashboardów (spec JSON → HTML, katalog komponentów)
  04_01_garden/             ← Ćwiczenie: cyfrowy ogród (agent z vault Markdown, narzędzia, umiejętności, workflow)
  04_04_system/             ← Ćwiczenie: multi-agent system (baza wiedzy Markdown, delegacja, workflow daily-news)
  04_05_review/            ← Ćwiczenie: laboratorium recenzji Markdown (komentarze inline, accept/reject/revert)
  04_05_apps/              ← Ćwiczenie: agent marketingowy (todos, kampanie, sprzedaż, kupony, produkty + UI)
  05_01_agent_graph/       ← Ćwiczenie: multi-agent graph (orkiestrator + specjaliści, pamięć, scheduler, dashboard)
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
| `System.Net.Http` | 4.3.4 | Klient HTTP do Responses API |
| `System.Configuration` | wbudowany w .NET 4.8 | Odczyt App.config |
| `System.Data.SQLite.Core` | 1.0.119 | Baza danych SQLite (FTS5, sesje agenta) |
| `Jint` | 4.0.0 | Silnik JavaScript (sandbox JS w agencie MCP) |
| `Neo4j.Driver` | 5.28.4 | Sterownik bazy grafowej Neo4j |
| `DiffPlex` | 1.7.2 | Porównywanie plików (diff) w serwerze MCP |
