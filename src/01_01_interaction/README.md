# Lesson 01 – Interaction

## Cel ćwiczenia
Demonstracja **wieloturowej rozmowy** z modelem językowym poprzez ręczne utrzymywanie
historii wiadomości i przekazywanie jej z każdym kolejnym wywołaniem API.

## Oryginał JS
`i-am-alice/4th-devs` → `01_01_interaction/app.js`

## Jak działa
1. Zadajemy pierwsze pytanie (`"What is 25 * 48?"`).
2. Odpowiedź modelu + oryginalne pytanie trafiają do historii.
3. Drugie pytanie (`"Divide that by 4."`) wysyłamy razem z historią –
   model "pamięta" kontekst i odpowiada poprawnie.

## Uruchomienie

### Wymagania
- Visual Studio 2017+ lub MSBuild 15+
- .NET Framework 4.8
- Klucz API (OpenAI lub OpenRouter)

### Konfiguracja
```
# w katalogu src/Lesson01_Interaction/
copy App.config.example App.config
# następnie otwórz App.config i uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY
```

### Budowanie i uruchomienie
```powershell
# z katalogu głównego repozytorium
dotnet run --project src/Lesson01_Interaction/Lesson01_Interaction.csproj
# lub z poziomu Visual Studio: ustaw Lesson01_Interaction jako projekt startowy i naciśnij F5
```

### Oczekiwany wynik
```
Q: What is 25 * 48?
A: 25 × 48 = 1200  (... reasoning tokens)

Q: Divide that by 4.
A: 1200 ÷ 4 = 300  (... reasoning tokens)
```
