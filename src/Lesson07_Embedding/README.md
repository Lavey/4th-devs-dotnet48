# Lesson 07 – Embedding

## Cel ćwiczenia
Interaktywne demo osadzania tekstu (embedding) z **kolorową macierzą podobieństwa parami** (cosine similarity).

## Oryginał JS
`i-am-alice/4th-devs` → `02_02_embedding/app.js`

## Jak działa
1. Otwiera interaktywny REPL
2. Każdy wpisany tekst jest osadzany modelem `text-embedding-3-small`
3. Po wpisaniu co najmniej dwóch tekstów wyświetla macierz podobieństwa kosinusowego
4. Kolorowanie: zielony ≥ 0.60 (podobne), żółty ≥ 0.35 (powiązane), czerwony < 0.35 (dalekie)

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson07_Embedding/Lesson07_Embedding.csproj
```

### Oczekiwany wynik
```
Embedding + Similarity Matrix (model: text-embedding-3-small)
Type 'exit' or press Enter on an empty line to quit.

Text: apple
  "apple" → [0.0123, …, -0.0456] (1536d)
  Add more to see similarities.

Text: orange

        apple   orange
apple     ——   █████ 0.78
orange  █████ 0.78     ——

  Legend:  ███ ≥0.60 similar  ███ ≥0.35 related  ███ <0.35 distant
```

## Uwagi
Wpisz `exit` lub naciśnij Enter na pustej linii, aby zakończyć.
