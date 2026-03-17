# Lesson 07 – Hybrid RAG

## Cel ćwiczenia
Agent RAG z **hybrydowym wyszukiwaniem** łączącym pełnotekstowe przeszukiwanie BM25 (SQLite FTS5)
z semantycznym przeszukiwaniem wektorów (cosine similarity) i fuzją RRF.

## Oryginał JS
`i-am-alice/4th-devs` → `02_02_hybrid_rag/app.js`

## Jak działa
1. Wczytuje pliki `.md` / `.txt` z `workspace/`, dzieli je na fragmenty i generuje embeddingi
2. Zapisuje wszystko w SQLite (tabele FTS5 + blobs wektorów)
3. Na zapytanie łączy przeszukiwanie BM25 (słowa kluczowe) z cosine similarity (semantyczne) przez RRF
4. Uruchamia pętlę agentową z historią konwersacji

## Wymagania
Oprócz standardowych kluczy API potrzebne są uprawnienia do generowania embeddingów:
- `OPENAI_API_KEY` z dostępem do `text-embedding-3-small`
- lub `OPENROUTER_API_KEY` (OpenRouter przekierowuje do `openai/text-embedding-3-small`)

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Dodaj dokumenty
Umieść swoje pliki `.md` lub `.txt` w `workspace/`.
Dołączony plik `workspace/knowledge.md` zawiera przykładowy dokument o RAG.

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson07_HybridRag/Lesson07_HybridRag.csproj
```

### Komendy REPL
- `exit` — zakończ
- `clear` — wyczyść historię konwersacji
- `reindex` — ponownie zindeksuj workspace

## Uwagi
Baza SQLite jest tworzona automatycznie w `data/hybrid.db` w katalogu wyjściowym.
SQLite FTS5 jest wbudowane w System.Data.SQLite.Core. Wyszukiwanie wektorów
jest realizowane w pamięci (bez sqlite-vec), co jest wystarczające dla demo.
