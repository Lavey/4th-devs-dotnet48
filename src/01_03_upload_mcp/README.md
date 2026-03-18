# Lesson 03 – MCP Upload Agent

## Cel ćwiczenia
Agent uploadujący, który skanuje folder `workspace/`, przesyła pliki przez HTTP
i prowadzi rejestr w `uploaded.md`. Demonstracja wzorca **wielokrotnych serwerów MCP**:
narzędzia plikowe + zdalny serwis uploadowy.

## Oryginał JS
`i-am-alice/4th-devs` → `01_03_upload_mcp/app.js`

## Jak działa
1. Odczytuje `workspace/uploaded.md` (ledger wcześniej uploadowanych plików)
2. Skanuje `workspace/` w poszukiwaniu plików, których jeszcze nie ma w ledgerze
3. Uploaduje każdy nowy plik przez HTTP (`UPLOAD_ENDPOINT` w App.config)
4. Aktualizuje `uploaded.md` z URL-em i datą przesłania

## Tryb dry-run
Bez ustawionego `UPLOAD_ENDPOINT` agent działa w trybie dry-run — loguje
co by uploadował, ale nie wykonuje żadnych żądań sieciowych.

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
# opcjonalnie: ustaw UPLOAD_ENDPOINT (np. UploadThing API URL)
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson03_UploadMcp/Lesson03_UploadMcp.csproj
```

### Oczekiwany wynik (dry-run)
```
=== MCP Upload Agent ===
Note: UPLOAD_ENDPOINT not set — running in dry-run mode.

  [tool] fs_list({"path":"."}) → {"entries":[...]}
  [tool] fs_read({"path":"uploaded.md"}) → {...}
  [dry-run] would upload: note1.txt → https://utfs.io/f/dry-run/note1.txt
  ...

Upload complete: 2 files uploaded, 1 skipped.
```
