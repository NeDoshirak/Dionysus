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

После успешной транскрипции API автоматически создаёт один анализ спецификации
и ставит его в фоновую очередь. Анализ проходит статусы `Queued`,
`RunningStage0`, `RunningStage1`, `RunningStage2`, `RunningStage3`, а затем
`Completed` или `Failed`. `GET /api/projects/{projectId}/specification`
возвращает текущий статус, ошибку (если есть), даты запуска/завершения,
бизнес-контекст, функции и элементы спецификации.

Обработка выполняется последовательно по этапам. Этап 3 запускает не более
четырёх запросов к AI одновременно; порядок опубликованных функций остаётся
стабильным. Сбой сохраняет статус `Failed` и описание этапа. Повторный запуск
разрешён только для `Failed`: `POST /api/projects/{projectId}/specification/retry`
создаёт новый `runId`, увеличивает `retryCount`, очищает неполные результаты и
возвращает анализ в `Queued`. Завершённый анализ повторно запустить нельзя.

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

CI запускается на push и pull request: проверяет Docker, полный backend test
project (с PostgreSQL для persistence-тестов), backend/frontend build,
`docker build ./backend` и `docker compose config`. Для deploy нужны secrets
`DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_PATH`; сервер должен
иметь Docker Compose и checkout этого репозитория.

## Авторизация API

Перед запуском задайте `JWT_KEY` и `AUTH_CODE_PEPPER` в `.env` случайными секретами, а также SMTP-переменные. API использует PostgreSQL для пользователей и Valkey для кодов и refresh-сессий.

- `POST /api/auth/register` — регистрация и письмо с кодом;
- `POST /api/auth/verify-email` — подтверждение кода и выдача токена;
- `POST /api/auth/login`, `/refresh`, `/logout`;
- `POST /api/auth/change-password` — смена пароля по текущему и новому паролю;
- `POST /api/auth/password-reset/request` и `/confirm`;
- `GET /api/auth/me` с `Authorization: Bearer <access-token>`.

Refresh-токен хранится в `HttpOnly` cookie с `SameSite=Strict` и путём `/api/auth/refresh`.
В Production cookie имеет флаг `Secure`; в Development флаг отключён, чтобы Swagger на
`http://localhost` мог выполнять refresh. Сброс пароля отзывает все активные refresh-сессии.

## Поиск

- `GET /api/projects/search?query=...` — нечёткий поиск проектов текущего пользователя по названию;
- `GET /api/projects/{projectId}/transcription-search?query=...` — нечёткий поиск по сегментам транскрипции внутри одного проекта.
- `GET /api/projects/{projectId}/recordings/{recordingId}/stream` — аудиопоток для проигрывателя с поддержкой Range-запросов.

Обе ручки возвращают score релевантности и требуют access token. Поиск учитывает
опечатки и похожие слова, а результаты сортируются от наиболее подходящих.

## Спецификация и происхождение данных

Все ручки спецификации требуют access token и ограничены владельцем проекта:

- `GET /api/projects/{projectId}/specification` — статус, ошибка, функции,
  элементы и бизнес-контекст анализа;
- `POST /api/projects/{projectId}/specification/retry` — повторить только
  неуспешный анализ;
- `GET /api/projects/{projectId}/specification/functions/{functionId}` —
  получить функцию;
- `POST/PATCH/DELETE /api/projects/{projectId}/specification/functions` и
  `/functions/{functionId}` — редактировать функции завершённого анализа;
- `POST/PATCH/DELETE /api/projects/{projectId}/specification/functions/{functionId}/items`
  и `/items/{itemId}` — редактировать элементы завершённого анализа.

Ответы содержат ссылки на исходные statements и transcript segments. Для
каждого сегмента возвращаются сохранённые `startSeconds`, `endSeconds` и текст,
поэтому UI может показать точное место в записи. AI-элементы сохраняют
происхождение, а созданные вручную элементы помечаются `isManual=true`.
Публикация результата атомарна: частичные функции и элементы не выдаются как
завершённая спецификация.

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
