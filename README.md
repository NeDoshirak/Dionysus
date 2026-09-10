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

Распознавание выполняется при создании проекта: `POST /api/projects` принимает
`multipart/form-data` с полями `name` и `media`. Поддерживаются аудио и видео;
для видео сохраняется только извлечённая аудиодорожка.

В ответе `GET /api/projects/{id}` транскрипция содержит сегменты для таймлайна:

```json
{
  "startSeconds": 12.4,
  "endSeconds": 16.8,
  "text": "текст сегмента"
}
```

По умолчанию Compose использует модель `small`, движок `faster_whisper`, русский
язык (`WHISPER_LANGUAGE=ru`) и VAD-фильтрацию. Для качества/скорости можно
изменить `WHISPER_MODEL`, `WHISPER_ENGINE` и `WHISPER_LANGUAGE` в `.env`.

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

## Поиск

- `GET /api/projects/search?query=...` — нечёткий поиск проектов текущего пользователя по названию;
- `GET /api/projects/{projectId}/transcription-search?query=...` — нечёткий поиск по сегментам транскрипции внутри одного проекта.

Обе ручки возвращают score релевантности и требуют access token. Поиск учитывает
опечатки и похожие слова, а результаты сортируются от наиболее подходящих.

## Yandex AI Studio

Для работы с DeepSeek v4 Flash задайте в `.env` `YANDEX_AI_API_KEY`,
`YANDEX_AI_FOLDER_ID` и `YANDEX_AI_MODEL`. Ключ используется только backend и
не передаётся frontend.

`POST /api/ai/respond` принимает JSON `{ "input": "...", "instructions": "..." }`
и возвращает текст ответа модели. Ручка требует access token.

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
