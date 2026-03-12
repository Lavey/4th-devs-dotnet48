# Lesson 03 – MCP Native

## Cel ćwiczenia
Demonstracja **jednego agenta używającego zarówno narzędzi MCP, jak i natywnych**
w tej samej pętli. Model nie wie, które narzędzia są "MCP", a które są "natywne" —
wszystkie wyglądają identycznie z perspektywy LLM.

## Oryginał JS
`i-am-alice/4th-devs` → `01_03_mcp_native/app.js`

## Narzędzia

| Narzędzie      | Typ     | Opis |
|----------------|---------|------|
| `get_weather`  | MCP     | Dane pogodowe dla miasta |
| `get_time`     | MCP     | Aktualny czas w danej strefie czasowej |
| `calculate`    | natywne | Podstawowe działania matematyczne |
| `uppercase`    | natywne | Zamiana tekstu na wielkie litery |

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson03_McpNative/Lesson03_McpNative.csproj
```

### Oczekiwany wynik
```
Q: What's the weather in Tokyo?
  [mcp] get_weather({"city":"Tokyo"}) → {"city":"Tokyo","weather":{"temp":22,...}}
A: The weather in Tokyo is currently 22°C and sunny.

Q: Calculate 42 multiplied by 17
  [native] calculate({"operation":"multiply","a":42,"b":17}) → {"result":714.0}
A: 42 multiplied by 17 equals 714.
...
```
