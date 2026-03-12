# Lesson 02 – Tools (Function Calling)

## Cel ćwiczenia
Demonstracja **wywoływania funkcji przez model** (function calling).
Model dostaje definicje narzędzi i sam decyduje, kiedy i z jakimi argumentami
je wywołać. My wykonujemy narzędzia lokalnie i odsyłamy wyniki do modelu.

## Oryginał JS
`i-am-alice/4th-devs` → `01_02_tools/app.js`

## Jak działa
1. Definiujemy dwa narzędzia:
   - `get_weather(location)` – zwraca zakodowane dane pogodowe
   - `send_email(to, subject, body)` – mockuje wysłanie e-maila
2. Wysyłamy pytanie: _"Check the current weather in Kraków. Then send a short email with the answer."_
3. Model odpowiada wywołaniem `get_weather` → wykonujemy narzędzie → odsyłamy wynik.
4. Model wywołuje `send_email` → wykonujemy narzędzie → odsyłamy wynik.
5. Model formułuje końcową odpowiedź tekstową.

## Uruchomienie

### Konfiguracja
```
copy ..\..\App.example.config App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson02_Tools/Lesson02_Tools.csproj
```

### Oczekiwany wynik
```
Q: Check the current weather in Kraków. Then send a short email with the answer to student@example.com.

  [tool] get_weather({"location":"Kraków"}) → {"temp":-2,"conditions":"snow"}
  [tool] send_email({"to":"student@example.com","subject":"...","body":"..."}) → {"success":true,...}

A: The current weather in Kraków is -2°C with snow. I've sent the email to student@example.com.
```
