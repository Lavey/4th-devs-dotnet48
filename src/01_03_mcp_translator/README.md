# Lesson 03 – MCP Translator

## Cel ćwiczenia
Agent tłumaczący, który **obserwuje folder** `workspace/translate/` i zapisuje
angielskie wersje plików do `workspace/translated/`. Demonstracja wzorca MCP:
narzędzia plikowe (fs_read, fs_write) + translacja przez LLM.

## Oryginał JS
`i-am-alice/4th-devs` → `01_03_mcp_translator/app.js`

## Jak działa
1. Skanuje `workspace/translate/` w poszukiwaniu plików tekstowych
2. Pomija pliki już przetłumaczone
3. Tłumaczy każdy plik na angielski zachowując formatowanie i ton
4. Zapisuje wynik do `workspace/translated/`

## Obsługiwane formaty
`.txt`, `.md`, `.html`, `.json`, `.csv`

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Dodaj pliki do tłumaczenia
```
# Wstaw pliki tekstowe do workspace/translate/
# Przy pierwszym uruchomieniu zostanie utworzony plik przykładowy przyklad.md
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson03_McpTranslator/Lesson03_McpTranslator.csproj
```

### Oczekiwany wynik
```
=== MCP Translator Agent ===
Source:      ...workspace\translate
Destination: ...workspace\translated

Created sample file: workspace/translate/przyklad.md

Translating: przyklad.md ...
  → przyklad.md

Done. Translated: 1  Skipped: 0
```
