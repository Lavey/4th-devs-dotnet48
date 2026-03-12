# Lesson 01 – Structured Output

## Cel ćwiczenia
Demonstracja **ustrukturyzowanego wyjścia JSON** z modelu językowego.
Model otrzymuje schemat JSON i musi odpowiedzieć dokładnie zgodnie z jego kształtem –
żadnych pól extra, żadnych brakujących wartości.

## Oryginał JS
`i-am-alice/4th-devs` → `01_01_structured/app.js`

## Jak działa
1. Definiujemy schemat JSON Schema opisujący obiekt `PersonData`
   (name, age, occupation, skills).
2. Schemat trafia do pola `text.format` żądania API.
3. Model zwraca JSON spełniający schemat; deserializujemy go do klasy `PersonData`.

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson01_Structured/Lesson01_Structured.csproj
```

### Oczekiwany wynik
```
Name:       John
Age:        30
Occupation: software engineer
Skills:     JavaScript, Python, React
```
