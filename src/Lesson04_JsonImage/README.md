# Lesson 04 – JSON Image

## Cel ćwiczenia
**Token-efektywne generowanie obrazów z szablonów JSON.**
Parametry stylu (palette, background, lighting, itp.) są przechowywane raz
w szablonie — tylko pole `subject` zmienia się per-generacja. Podejście to
sprawia, że prompty są reprodukowalne i kontrolowalne przez VCS.

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_json_image/app.js`

## Jak działa
1. Czyta `workspace/template.json` (tworzy domyślny przy pierwszym uruchomieniu)
2. Kopiuje szablon do `workspace/prompts/<subject_slug>.json`
3. Edytuje tylko pole `subject` w kopii
4. Buduje pełny prompt z pól JSON i generuje obraz (DALL-E 3)
5. Zapisuje do `workspace/output/`

## Wymagania
- `OPENAI_API_KEY` — wymagany

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# wymagane: OPENAI_API_KEY
```

### Budowanie i uruchomienie
```powershell
# Demo (3 domyślne subjects)
dotnet run --project src/Lesson04_JsonImage/Lesson04_JsonImage.csproj

# Własny subject
dotnet run --project src/Lesson04_JsonImage/Lesson04_JsonImage.csproj -- "a cyberpunk samurai"
```

### Oczekiwany wynik
```
=== JSON Image Agent ===

Template: workspace/template.json

Subject: a friendly robot explorer
Prompt file: workspace/prompts/a_friendly_robot_explorer.json
Generating image ...
Saved: workspace/output/a_friendly_robot_explorer_20260312_194500.png

Subject: a wise old wizard with a glowing staff
...

Done.
```
