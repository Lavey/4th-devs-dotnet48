# Lesson 04 – Image Guidance

## Cel ćwiczenia
**Generowanie obrazów oparte na szablonie JSON i obrazie referencyjnym pozy.**
Agent czyta `workspace/template.json` z parametrami stylu i opcjonalnie obraz
referencyjny z `workspace/reference/`, a następnie generuje finalny obraz.

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_image_guidance/app.js`

## Jak działa
1. Czyta `workspace/template.json` (tworzy domyślny przy pierwszym uruchomieniu)
2. Akceptuje opis postaci/obiektu (argument CLI lub wartość domyślna)
3. Opcjonalnie: używa obrazu referencyjnego z `workspace/reference/` dla zachowania pozy
4. Generuje obraz przez DALL-E 3 i zapisuje do `workspace/output/`

## Wymagania
- `OPENAI_API_KEY` — wymagany

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# wymagane: OPENAI_API_KEY
```

### Opcjonalne
```
# Dodaj obraz referencyjny pozy do workspace/reference/ (np. walking-pose.png)
# Dostosuj workspace/template.json
```

### Budowanie i uruchomienie
```powershell
# Domyślny subject
dotnet run --project src/Lesson04_ImageGuidance/Lesson04_ImageGuidance.csproj

# Własny subject
dotnet run --project src/Lesson04_ImageGuidance/Lesson04_ImageGuidance.csproj -- "a running knight with a red cape"
```

### Oczekiwany wynik
```
=== Image Guidance Agent ===

Template loaded: workspace/template.json
Subject: a female magician in a walking pose
Pose reference: walking-pose.png

Generating image ...
Image saved: workspace\output\guided_20260312_194500.png

Done.
```
