# Dionysus
# Dionysus

Минимальный full-stack каркас: Vite + React frontend, ASP.NET Core 8 API, PostgreSQL и Whisper для распознавания речи.

## Запуск

```bash
cp .env.example .env
docker compose up --build
```

- Frontend: http://localhost:3000
- API: http://localhost:8000/docs
- API health: http://localhost:8000/health
- Whisper: http://localhost:9000

Распознавание: `POST /api/transcribe` с multipart-полем `file` и audio-файлом.

## CI/CD

CI запускается на push и pull request. Для deploy нужны secrets `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_PATH`; сервер должен иметь Docker Compose и checkout этого репозитория.

Первый запуск Whisper может скачать модель и занять несколько минут. Размер модели настраивается через `WHISPER_MODEL` (`tiny`, `base`, `small`, `medium`, `large`).
