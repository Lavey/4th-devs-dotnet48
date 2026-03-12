# Lesson 02 – Tool Use (Sandboxed Filesystem)

## Cel ćwiczenia
Demonstracja **wywoływania narzędzi systemu plików przez model**.
Model otrzymuje zestaw narzędzi do zarządzania plikami wewnątrz piaskownicy –
wszystkie operacje są ograniczone do katalogu `sandbox/` i nie mogą wyjść poza niego
(path traversal jest blokowany na poziomie narzędzi).

## Oryginał JS
`i-am-alice/4th-devs` → `01_02_tool_use/app.js`

## Dostępne narzędzia
| Narzędzie          | Opis                                              |
|--------------------|---------------------------------------------------|
| `list_files`       | Listuje pliki i katalogi w podanej ścieżce        |
| `read_file`        | Czyta zawartość pliku tekstowego                  |
| `write_file`       | Tworzy lub nadpisuje plik                         |
| `delete_file`      | Usuwa plik                                        |
| `create_directory` | Tworzy katalog (rekurencyjnie)                    |
| `file_info`        | Zwraca metadane pliku (rozmiar, daty)             |

## Bezpieczeństwo
Każde wywołanie narzędzia sprawdza, czy ścieżka nie wychodzi poza katalog `sandbox/`.
Próba odczytu `../config.js` zostanie odrzucona z błędem "Access denied".

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# uzupełnij OPENAI_API_KEY lub OPENROUTER_API_KEY w App.config
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson02_ToolUse/Lesson02_ToolUse.csproj
```

### Oczekiwany wynik
Program wykona serię zapytań demonstrujących każde narzędzie, m.in.:
```
Q: Create a file called hello.txt with content: 'Hello, World!'
  [tool] write_file({"path":"hello.txt","content":"Hello, World!"}) → {"success":true,...}
A: I have created hello.txt with the content 'Hello, World!'.

Q: Try to read ../config.js
  [tool] read_file({"path":"../config.js"}) → {"error":"Access denied: path outside sandbox."}
A: Access was denied – the path is outside the sandbox.
```
