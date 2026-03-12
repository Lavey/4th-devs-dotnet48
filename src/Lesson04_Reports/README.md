# Lesson 04 – Reports

## Cel ćwiczenia
Agent do **analizy dokumentów i generowania raportów strukturalnych**.
Odkrywa pliki w `workspace/docs/`, czyta ich zawartość przez narzędzia
i pisze kompleksowy raport Markdown do `workspace/output/`.

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_reports/app.js`

## Jak działa
1. Wyświetla dostępne dokumenty w `workspace/docs/`
2. Czyta treść każdego dokumentu przez narzędzie `read_document`
3. Generuje ustrukturyzowany raport Markdown z:
   - Podsumowaniem wykonawczym
   - Kluczowymi ustaleniami per dokument
   - Motywami przekrojowymi
   - Rekomendacjami
4. Zapisuje raport przez narzędzie `write_report`

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Przygotowanie danych
```
# Dodaj pliki .md/.txt do workspace/docs/
# Przy pierwszym uruchomieniu zostaną utworzone 3 przykładowe dokumenty
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson04_Reports/Lesson04_Reports.csproj
```

### Oczekiwany wynik
```
=== Reports Agent ===

Created 3 sample documents in workspace/docs/

  [tool] list_documents({}) → {"documents":[...],"count":3}
  [tool] read_document({"filename":"q1_sales.md"}) → ...
  [tool] read_document({"filename":"customer_feedback.md"}) → ...
  [tool] read_document({"filename":"tech_roadmap.md"}) → ...
  [tool] write_report({"filename":"report.md","content":"# ..."}) → ...

Report saved: workspace/output/report.md
```
