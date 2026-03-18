# Lesson 01 – Grounding

## Cel ćwiczenia
Demonstracja **gruntowania faktów** – model czyta notatki Markdown i generuje
raport HTML z adnotacjami weryfikacyjnymi przy każdym twierdzeniu, które można sprawdzić.

## Oryginał JS
`i-am-alice/4th-devs` → `01_01_grounding/app.js`

## Jak działa
1. Wczytujemy plik Markdown z katalogu `notes/`.
2. Wysyłamy całą treść do modelu z prośbą o konwersję na HTML
   i oznaczenie każdego weryfikowalnego faktu adnotacją `[?]`.
3. Wynik zapisujemy do `output/grounded_report.html`.

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
# domyślnie użyje notes/notes.md
dotnet run --project src/Lesson01_Grounding/Lesson01_Grounding.csproj

# lub z własnym plikiem
dotnet run --project src/Lesson01_Grounding/Lesson01_Grounding.csproj -- path/to/my_notes.md
```

### Oczekiwany wynik
Plik `output/grounded_report.html` z treścią notatek przekształconych w HTML
z oznaczeniami `[?]` przy faktach wymagających weryfikacji.
