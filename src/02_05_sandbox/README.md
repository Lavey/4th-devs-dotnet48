# 02_05_sandbox

Port ćwiczenia [`02_05_sandbox`](https://github.com/i-am-alice/4th-devs/tree/main/02_05_sandbox) na C# / .NET Framework 4.8.

Agent MCP sandbox z dynamicznym odkrywaniem narzędzi i wykonywaniem kodu JavaScript.

## Co robi

1. Agent ładuje szablon z `workspace/agents/sandbox.agent.md`.
2. Używa czterech narzędzi do realizacji zadania:
   - **list_servers** – wyświetla dostępne serwery MCP (wbudowany serwer `todo`)
   - **list_tools** – listuje narzędzia serwera (create, get, list, update, delete)
   - **get_tool_schema** – ładuje deklarację TypeScript wybranego narzędzia
   - **execute_code** – wykonuje kod JavaScript w izolowanym sandboxie [Jint](https://github.com/sebastienros/jint)
3. Kod JS może synchronicznie wywoływać załadowane narzędzia MCP (`todo.create(...)`, `todo.list(...)`, itd.).
4. Wbudowany serwer todo przechowuje elementy w pamięci (nie wymaga osobnego procesu).

## Wymagania

- .NET Framework 4.8 + .NET SDK 6+
- Klucz API OpenAI lub OpenRouter

## Konfiguracja

```powershell
copy App.config.example App.config
# Uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

## Uruchamianie

```powershell
# Domyślne zadanie (lista zakupów)
dotnet run --project src\02_05_sandbox\02_05_sandbox.csproj

# Własne zadanie jako argument
dotnet run --project src\02_05_sandbox\02_05_sandbox.csproj -- "Create two todos: walk the dog and read a book. Mark the first one complete."
```

## Różnice względem oryginału JS

| Aspekt | Oryginał (TypeScript) | Port (.NET 4.8) |
|--------|----------------------|-----------------|
| Silnik JS sandbox | QuickJS (WASM asyncify) | [Jint](https://github.com/sebastienros/jint) 3.x |
| Serwer todo | Osobny proces MCP (stdio) | Wbudowany w pamięci C# |
| LLM API | Chat Completions | Responses API |
| Model | `openai:gpt-4.1` | `openai:gpt-4.1` |

## Przykładowy przebieg

```
Task: Create a shopping list with: milk, bread, eggs.
      Then mark milk as completed and show me what's left to buy.

[sandbox] Tool: list_servers({})
[sandbox]   → [{"name":"todo","description":"..."}]

[sandbox] Tool: list_tools({"server":"todo"})
[sandbox]   → [{"Name":"create",...},{"Name":"list",...},...]

[sandbox] Tool: get_tool_schema({"server":"todo","tool":"create"})
[sandbox]   → TypeScript definition loaded: ...

[sandbox] Tool: get_tool_schema({"server":"todo","tool":"update"})
[sandbox]   → TypeScript definition loaded: ...

[sandbox] Tool: get_tool_schema({"server":"todo","tool":"list"})
[sandbox]   → TypeScript definition loaded: ...

[sandbox] Tool: execute_code({"code":"..."})
[sandbox] Output:
  Remaining items to buy:
  - bread
  - eggs

[sandbox] Completed

Result: Your shopping list has been created...
```
