# Lesson 04 – Audio

## Cel ćwiczenia
Demonstracja **przetwarzania audio** przez OpenAI API:
- **Transkrypcja** — model Whisper (`POST /audio/transcriptions`)
- **Text-to-Speech** — model TTS-1 (`POST /audio/speech`)

## Oryginał JS
`i-am-alice/4th-devs` → `01_04_audio/app.js`

## Jak działa
1. Skanuje `workspace/input/` w poszukiwaniu plików audio (`.mp3`, `.wav`, `.m4a`, itd.)
2. Transkrybuje każdy znaleziony plik i zapisuje wynik do `workspace/output/`
3. Generuje przykładowy plik MP3 przez TTS i zapisuje jako `workspace/output/tts_demo.mp3`

## Wymagania
- Klucz `OPENAI_API_KEY` (endpointy `/audio/*` są dostępne tylko przez OpenAI)
- Pliki audio w `workspace/input/` (opcjonalne — TTS demo działa bez nich)

## Uruchomienie

### Konfiguracja
```
copy App.config.example App.config
# wymagane: OPENAI_API_KEY
```

### Dodaj plik audio (opcjonalnie)
```
# Skopiuj plik .mp3/.wav/.m4a do workspace/input/
```

### Budowanie i uruchomienie
```powershell
dotnet run --project src/Lesson04_Audio/Lesson04_Audio.csproj
```

### Oczekiwany wynik
```
=== Audio Processing Demo ===

--- Transcription (Whisper) ---
Transcribing: sample.mp3 ...
Transcript: Hello, this is a test recording...
Saved: workspace\output\sample_transcript.txt

--- Text-to-Speech (TTS) ---
Generating speech ...
Saved: workspace\output\tts_demo.mp3

Done.
```
