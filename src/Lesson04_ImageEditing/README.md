# Lesson 04 – Image Editing

## Cel ćwiczenia
Agent do **generowania i edytowania obrazów** z kontrolą jakości przez model vision.
Czyta przewodnik stylistyczny, generuje lub edytuje obraz, a następnie recenzuje wynik.

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_image_editing/app.js`

## Jak działa
1. Czyta `workspace/style-guide.md` przed generowaniem
2. Jeśli `workspace/input/` zawiera obraz — edytuje go (DALL-E 2 edit)
3. W przeciwnym razie generuje nowy obraz od podstaw (DALL-E 3)
4. Analizuje wynik modelem gpt-4o (vision) pod kątem zgodności i jakości
5. Zapisuje finalny obraz do `workspace/output/`

## Wymagania
- `OPENAI_API_KEY` — wymagany (DALL-E + Whisper są niedostępne przez OpenRouter)

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# wymagane: OPENAI_API_KEY
```

### Opcjonalne
```
# Dodaj obraz PNG do workspace/input/ aby przetestować tryb edycji
# Dostosuj workspace/style-guide.md
```

### Budowanie i uruchomienie
```powershell
# Domyślny prompt
dotnet run --project src/Lesson04_ImageEditing/Lesson04_ImageEditing.csproj

# Własny prompt
dotnet run --project src/Lesson04_ImageEditing/Lesson04_ImageEditing.csproj -- "A watercolour painting of a cat reading a book"
```

### Oczekiwany wynik
```
=== Image Editing Agent ===

Style guide loaded.
Request: Create a monochrome concept sketch of a futuristic motorcycle

No source image found — generating from scratch ...

Image saved: workspace\output\generated_20260312_194000.png

Reviewing result with vision ...

Review:
The image successfully depicts a futuristic motorcycle in monochrome...

Done.
```
