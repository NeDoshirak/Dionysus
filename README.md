# Dionysus
# Dionysus

Минимальный full-stack каркас: Vite + React frontend, ASP.NET Core 8 API, PostgreSQL и Whisper для распознавания речи.

## Запуск

```bash
cp .env.example .env
docker compose up --build
```

- Frontend: http://localhost:3000
- API: http://localhost:8000/swagger/index.html
- API health: http://localhost:8000/health
- Whisper: http://localhost:9000

Распознавание: `POST /api/transcribe` с multipart-полем `file` и audio-файлом.

## CI/CD

CI запускается на push и pull request. Для deploy нужны secrets `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_PATH`; сервер должен иметь Docker Compose и checkout этого репозитория.

## Авторизация API

Перед запуском задайте `JWT_KEY` и `AUTH_CODE_PEPPER` в `.env` случайными секретами, а также SMTP-переменные. API использует PostgreSQL для пользователей и Valkey для кодов и refresh-сессий.

- `POST /api/auth/register` — регистрация и письмо с кодом;
- `POST /api/auth/verify-email` — подтверждение кода и выдача токена;
- `POST /api/auth/login`, `/refresh`, `/logout`;
- `POST /api/auth/password-reset/request` и `/confirm`;
- `GET /api/auth/me` с `Authorization: Bearer <access-token>`.

Refresh-токен хранится в `HttpOnly` cookie с `SameSite=Strict` и путём `/api/auth/refresh`.
В Production cookie имеет флаг `Secure`; в Development флаг отключён, чтобы Swagger на
`http://localhost` мог выполнять refresh. Сброс пароля отзывает все активные refresh-сессии.

Пример запроса refresh:

```bash
curl -i -X POST http://localhost:8000/api/auth/refresh \
  -b cookies.txt -c cookies.txt
```

Пример запроса сброса пароля:

```bash
curl -X POST http://localhost:8000/api/auth/password-reset/request \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'

curl -X POST http://localhost:8000/api/auth/password-reset/confirm \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","code":"123456","newPassword":"NewPass123!"}'
```

Первый запуск Whisper может скачать модель и занять несколько минут. Размер модели настраивается через `WHISPER_MODEL` (`tiny`, `base`, `small`, `medium`, `large`).
