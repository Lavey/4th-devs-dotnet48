# Lesson 05 – Confirmation (File & Email Agent)

## Cel ćwiczenia

Interaktywny agent **REPL** mogący:
1. Odczytywać, zapisywać, listować i przeszukiwać pliki w katalogu `workspace/`
2. Wysyłać e-maile przez API **Resend**

Kluczowa funkcja: przed wykonaniem `send_email` agent **zatrzymuje się i pyta użytkownika
o potwierdzenie** (human-in-the-loop). Można też „zaufać" narzędziu na czas sesji (opcja T),
co pozwoli pomijać potwierdzenia w dalszej części rozmowy.

## Oryginał JS

`i-am-alice/4th-devs` → `01_05_confirmation/app.js`

## Jak działa

1. Agent uruchamia pętlę REPL; użytkownik wpisuje zapytania po polsku lub angielsku.
2. Model może wywoływać narzędzia: `list_files`, `read_file`, `write_file`, `search_files`, `send_email`.
3. Przy próbie wywołania `send_email` pojawia się kolorowy panel potwierdzenia:
   - **Y** – wyślij jednorazowo
   - **T** – zaufaj narzędziu na tę sesję (auto-approve)
   - **N** – anuluj
4. Odbiorcy e-maili są walidowani względem `workspace/whitelist.json`
   (obsługuje dokładne adresy i wzorce domenowe, np. `@yourdomain.com`).
5. Historia rozmowy jest zachowywana między pytaniami; komenda `clear` ją zeruje.

## Uruchomienie

### Konfiguracja

```powershell
copy App.config.example App.config
# Uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
# Opcjonalnie: RESEND_API_KEY + RESEND_FROM (wymagane do wysyłania e-maili)
```

### Budowanie i uruchomienie

```powershell
dotnet run --project src/Lesson05_Confirmation/Lesson05_Confirmation.csproj
```

### Komendy REPL

| Komenda   | Działanie                                    |
|-----------|----------------------------------------------|
| `clear`   | Czyści historię rozmowy                      |
| `untrust` | Usuwa zaufane narzędzia (resetuje Trust mode)|
| `exit`    | Kończy program                               |

### Przykładowe zapytania

```
You: List all files in the workspace
You: Read workspace/docs/sample.md and summarise it
You: Write "Hello from the agent!" to workspace/output/hello.txt
You: Send an email to alice@aidevs.pl with subject "Hello" and a short greeting
You: Search for any markdown files in the workspace
```

## Konfiguracja whitelist

Edytuj `workspace/whitelist.json`:

```json
{
  "allowed_recipients": [
    "alice@aidevs.pl",
    "@yourdomain.com"
  ]
}
```

- Dokładny adres e-mail (`user@example.com`) – zezwala tylko temu adresowi
- Wzorzec domeny (`@example.com`) – zezwala wszystkim adresom z tej domeny
