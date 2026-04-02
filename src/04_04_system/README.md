# 04_04_system

Port ćwiczenia [`04_04_system`](https://github.com/i-am-alice/4th-devs/tree/main/04_04_system) na C# / .NET Framework 4.8.

Multi-agent system — baza wiedzy Markdown steruje zachowaniem agentów.
Workflow, szablony, reguły i tożsamości agentów to notatki w vault — nie kod.

## Uruchomienie

```powershell
# Interaktywny tryb — rozmowa z Alice o bazie wiedzy:
dotnet run --project src\04_04_system\04_04_system.csproj

# Workflow daily-news — pipeline: research → assemble → deliver:
dotnet run --project src\04_04_system\04_04_system.csproj -- daily-news

# Przykładowe zapytania — wszystkie 7:
dotnet run --project src\04_04_system\04_04_system.csproj -- examples

# Tylko zapytanie nr 3 (person):
dotnet run --project src\04_04_system\04_04_system.csproj -- examples 3
```

## Architektura

Pętla agentowa z delegacją (max 10 kroków, max głębokość 2).
Profile agentów ładowane z `workspace/system/agents/*.md` — każdy deklaruje model, narzędzia i prompt systemowy w frontmatter.

```
Alice (orchestrator)
├─ czyta workflow z workspace/ops/daily-news/
├─ deleguje fazę 1 → Ellie (research)
├─ deleguje fazę 2 → Tony (assemble)
└─ deleguje fazę 3 → Rose (deliver)
```

## Struktura bazy wiedzy

```
workspace/
├── me/          ← human-only: tożsamość, preferencje
├── world/       ← shared: ludzie, narzędzia, źródła
├── craft/       ← shared: wiedza, projekty, eksperymenty
├── ops/         ← agent-driven: workflow, output badań
└── system/      ← human-owned: profile agentów, szablony, reguły
```

## Narzędzia

| Narzędzie | Opis |
|-----------|------|
| `read_file` | Odczyt pliku z workspace |
| `write_file` | Zapis pliku do workspace |
| `list_dir` | Listowanie katalogu workspace |
| `search_files` | Wyszukiwanie tekstu w plikach workspace |
| `sum` | Dodawanie dwóch liczb |
| `send_email` | Symulowana wysyłka e-mail (zapis HTML do folderu output) |
| `delegate` | Delegacja zadania do innego agenta |

## Różnice względem oryginału JS

- Narzędzia plikowe (read/write/list/search) zaimplementowane bezpośrednio zamiast MCP files server
- Brak integracji web search (Firecrawl) — research agent korzysta z wbudowanej wiedzy modelu
- Profile agentów w `workspace/system/agents/*.md` (bez zmian, ten sam format frontmatter)
- Silnik JS zastąpiony natywnym C# — brak Node.js/npm dependency

## Demo

Katalog `demo/` zawiera przykładowe dane wyjściowe workflow daily-news:
- `ai.md`, `dev.md`, `startups.md` — notatki badawcze per temat
- `digest.html` — skompilowany digest HTML
- `status.md` — log dostarczenia
