# Lesson 04 – Image Recognition

## Cel ćwiczenia
Agent **klasyfikujący obrazy** przy użyciu modelu z obsługą widzenia (gpt-4o).
Czyta profile kategorii z `knowledge/*.md`, analizuje każdy obraz i kopiuje go
do odpowiedniego podfolderu.

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_image_recognition/app.js`

## Jak działa
1. Wczytuje profile kategorii z `knowledge/` (pliki `.md`)
2. Analizuje każdy obraz w `images/` przy użyciu vision API (gpt-4o)
3. Dopasowuje cechy wizualne do profili
4. Kopiuje obrazy do `images/organized/<kategoria>/`

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# wymagane: OPENAI_API_KEY lub OPENROUTER_API_KEY (z modelem obsługującym vision)
```

### Przygotowanie danych
```
# Dodaj obrazy .jpg/.png/.webp do folderu images/
# Dostosuj lub dodaj profile kategorii w knowledge/*.md
# Przy pierwszym uruchomieniu zostaną utworzone przykładowe profile
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson04_ImageRecognition/Lesson04_ImageRecognition.csproj
```

### Oczekiwany wynik
```
=== Image Recognition Agent ===
Classify images by character

Loaded profile: nature
Loaded profile: people
Loaded profile: architecture
Loaded profile: technology

Found 3 image(s) to classify.

Classifying: photo1.jpg ...
  → Category: nature
  → Copied to: images/organized/nature/photo1.jpg

Done. Classified: 3  Errors: 0
```
