# Lesson 09 – Daily Ops

## Cel ćwiczenia
System **wieloagentowy** generujący codzienną notatkę operacyjną (*Daily Ops*).
Agent orkiestrator czyta instrukcję workflow, deleguje zbieranie danych do
wyspecjalizowanych agentów (mail, calendar, tasks, notes), a następnie
syntetyzuje wynik w dokument Markdown zapisywany do `workspace/output/`.

## Oryginał JS
`i-am-alice/4th-devs` → `02_04_ops/src/`

## Jak działa
1. **Program.cs** pyta o potwierdzenie (zużycie tokenów), po czym uruchamia agenta `orchestrator`
2. **Orkiestrator** czyta workflow z `workspace/workflows/daily-ops.md`
3. Deleguje zadania do 4 agentów specjalistycznych:
   - **mail** – skanuje skrzynkę (`sources/mail.json`)
   - **calendar** – przegląda kalendarz (`sources/calendar.json`)
   - **tasks** – przegląda listę zadań (`sources/tasks.json`)
   - **notes** – przegląda notatki (`sources/notes.json`)
4. Czyta cele długoterminowe, historię z poprzedniego dnia i preferencje formatowania
5. Syntetyzuje wszystko w notatkę wg szablonu i zapisuje ją do `workspace/output/YYYY-MM-DD.md`

## Architektura agentów

```
orchestrator
├── delegate → mail       (tool: get_mail)
├── delegate → calendar   (tool: get_calendar)
├── delegate → tasks      (tool: get_tasks)
├── delegate → notes      (tool: get_notes)
├── read_file (workflow, goals, history, preferences)
└── write_file (output/YYYY-MM-DD.md)
```

## Struktura workspace

```
workspace/
  agents/          ← definicje agentów w Markdown z frontmatter YAML
  workflows/       ← daily-ops.md (instrukcja dla orkiestratora)
  sources/         ← przykładowe dane (mail.json, calendar.json, tasks.json, notes.json)
  goals/           ← goals.md (cele długoterminowe)
  history/         ← poprzednie Daily Ops (do deduplicacji i eskalacji)
  memory/          ← preferences.md (preferencje formatowania)
  output/          ← wygenerowane pliki YYYY-MM-DD.md
```

## Uruchomienie

### Konfiguracja
```powershell
copy App.example.config src\02_04_ops\App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src\02_04_ops\02_04_ops.csproj
```

### Oczekiwany wynik
Program zapyta o potwierdzenie, po czym uruchomi agenta. W konsoli zobaczysz
logi delegowania (kolor cyjanowy / magenta), wyniki narzędzi (szary) i
potwierdzenie ukończenia (zielony). Gotowy dokument Daily Ops zostanie zapisany
do `workspace/output/YYYY-MM-DD.md`.

```
========================================
  Daily Ops Generator — 2026-02-13
========================================

⚠️  UWAGA: Uruchomienie tego agenta może zużyć zauważalną liczbę tokenów.
   Jeśli nie chcesz uruchamiać go teraz, najpierw sprawdź plik demo:
   Demo: demo/example.md

Czy chcesz kontynuować? (yes/y): y
[orchestrator] Starting (depth: 0)
[orchestrator] Tool: read_file({"path":"workflows/daily-ops.md"})
[orchestrator] Delegating to [mail]: Gather inbox summary ...
[mail] Starting (depth: 1)
[mail] Tool: get_mail({})
[mail] Completed
...
[orchestrator] Tool: write_file({"path":"output/2026-02-13.md", ...})
[orchestrator] Completed

========================================
  Result
========================================

# Daily Ops — 2026-02-13 (Friday)
...
```

## Definicje agentów
Każdy agent jest opisany w pliku `workspace/agents/*.agent.md` z frontmatter YAML:
```yaml
---
name: mail
model: openai:gpt-4.1-mini
tools:
  - get_mail
---
You scan the inbox and return a structured summary.
...
```

## Narzędzia

| Narzędzie      | Opis                                              |
|----------------|---------------------------------------------------|
| `get_mail`     | Zwraca JSON z wiadomościami e-mail                |
| `get_calendar` | Zwraca JSON z wydarzeniami kalendarza             |
| `get_tasks`    | Zwraca JSON z listą zadań                         |
| `get_notes`    | Zwraca JSON z notatkami                           |
| `read_file`    | Czyta plik z workspace (ścieżka względna)         |
| `write_file`   | Zapisuje plik do workspace (ścieżka względna)     |
| `delegate`     | Deleguje zadanie do wybranego agenta specjalisty  |
