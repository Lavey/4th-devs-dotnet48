# Lesson 03 – MCP Core (Model Context Protocol)

## Cel ćwiczenia
Demonstracja **wszystkich podstawowych możliwości MCP** (Model Context Protocol):
- **Narzędzia (Tools)** – lista narzędzi i wywołania przez klienta
- **Zasoby (Resources)** – odkrywanie i odczyt danych tylko do odczytu
- **Szablony promptów (Prompts)** – odkrywanie i renderowanie szablonów z parametrami
- **Elicitation** – serwer prosi klienta o potwierdzenie od użytkownika
- **Sampling** – serwer deleguje wywołanie LLM z powrotem przez klienta

## Oryginał JS
`i-am-alice/4th-devs` → `01_03_mcp_core/app.js`

## Adaptacja .NET 4.8
Oryginalny JS spawns prawdziwy serwer MCP jako podproces przez transport stdio.
Ponieważ oficjalny SDK MCP dla C# wymaga .NET 6+, ta wersja implementuje
ekwiwalentną klasę `McpServer` in-process, która udostępnia te same narzędzia,
zasoby i szablony promptów przez ten sam konceptualny interfejs.

## Capabilities

| Typ       | Nazwa                          | Opis |
|-----------|--------------------------------|------|
| Tool      | `calculate`                    | Podstawowe działania arytmetyczne |
| Tool      | `summarize_with_confirmation`  | Podsumowanie tekstu po elicitation i sampling |
| Resource  | `config://project`             | Statyczna konfiguracja projektu |
| Resource  | `data://stats`                 | Dynamiczne statystyki runtime |
| Prompt    | `code-review`                  | Szablon przeglądu kodu z parametrami |

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson03_McpCore/Lesson03_McpCore.csproj
```

### Oczekiwany wynik
```
=== TOOLS — Actions the server exposes for the LLM to invoke ===

[listTools]
  calculate — Basic arithmetic ...
  summarize_with_confirmation — ...

[callTool(calculate)]
{ "result": 714.0 }

[MCP elicitation] Summarize text (max 30 words)? (y/n): y

[callTool(summarize_with_confirmation)]
{ "summary": "MCP is a protocol that standardises how applications provide context ..." }

=== RESOURCES — Read-only data the server makes available to clients ===
...
=== PROMPTS — Reusable message templates with parameters ===
...
```
