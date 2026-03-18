# Lesson 07 – Chunking

## Cel ćwiczenia
Porównanie **czterech strategii podziału tekstu** (chunking) stosowanych w systemach RAG.

## Oryginał JS
`i-am-alice/4th-devs` → `02_02_chunking/app.js`

## Jak działa
1. Wczytuje `workspace/example.md`
2. Uruchamia cztery strategie podziału na tym samym tekście:
   - **Characters** — stałe okna znakowe z nakładaniem (overlap)
   - **Separators** — podział na nagłówkach i akapitach (rekurencyjny)
   - **Context** — podziały oparte na separatorach, wzbogacone o prefiks kontekstowy generowany przez LLM
   - **Topics** — LLM identyfikuje logiczne granice tematyczne
3. Zapisuje każdy wynik jako JSONL w `workspace/example-[strategia].jsonl`

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson07_Chunking/Lesson07_Chunking.csproj
```

### Oczekiwany wynik
```
Source: example.md (20562 chars)

1. Characters...
  ✓ workspace/example-characters.jsonl (26 chunks)
2. Separators...
[separators] overlap trimmed: 2, dropped: 0
  ✓ workspace/example-separators.jsonl (13 chunks)
3. Context (LLM-enriched)...
  context: enriching 13/13
  ✓ workspace/example-context.jsonl (13 chunks)
4. Topics (AI-driven)...
  ✓ workspace/example-topics.jsonl (8 chunks)

Done.
```

## Uwagi
Strategie `characters` i `separators` są czysto lokalne.
Strategie `context` i `topics` wywołują LLM, więc zużywają tokeny.
Gotowe wyniki są już obecne w `workspace/`.
