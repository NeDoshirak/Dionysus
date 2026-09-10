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

## Production-развёртывание

Ниже описано развёртывание на одном сервере Ubuntu 22.04/24.04 с Docker Compose, системным Nginx и HTTPS от Let's Encrypt. Примеры используют домен `app.example.com` и каталог `/opt/dionysus`; замените их своими значениями.

```text
Интернет → Nginx :443 → frontend-container :3000
                              └→ backend-container :8000
                                   ├→ PostgreSQL
                                   ├→ Valkey
                                   └→ Whisper
```

Внешнему миру доступны только порты 80 и 443. PostgreSQL, Valkey, Whisper и API остаются доступными лишь внутри Docker-сети.

### 1. DNS и сервер

Создайте A-запись `app.example.com`, указывающую на публичный IPv4 сервера. Откройте SSH, HTTP и HTTPS:

```sh
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Установите Docker Engine с Compose plugin по [официальной инструкции Docker для Ubuntu](https://docs.docker.com/engine/install/ubuntu/), а затем Nginx и Certbot:

```sh
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx ca-certificates curl git
sudo systemctl enable --now docker nginx
docker --version
docker compose version
```

> Docker-порты могут обходить обычные правила UFW. Не публикуйте порты базы данных, Whisper, Valkey и backend на интерфейсе сервера.

### 2. Получить код и создать `.env`

```sh
sudo mkdir -p /opt/dionysus
sudo chown "$USER:$USER" /opt/dionysus
git clone <URL_РЕПОЗИТОРИЯ> /opt/dionysus
cd /opt/dionysus
cp .env.example .env
chmod 600 .env
```

Заполните `.env` уникальными секретами:

```env
POSTGRES_DB=dionysus
POSTGRES_USER=dionysus
POSTGRES_PASSWORD=<длинный-случайный-пароль>

WHISPER_MODEL=small
WHISPER_ENGINE=faster_whisper
WHISPER_LANGUAGE=ru

JWT_KEY=<случайный-ключ-минимум-32-байта>
AUTH_CODE_PEPPER=<отдельный-случайный-ключ>

SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=mail@example.com
SMTP_PASSWORD=<пароль-приложения-SMTP>
```

Для генерации секрета можно использовать `openssl rand -base64 48`. Никогда не коммитьте `.env`, не передавайте его в логи и не используйте тестовые значения в production.

### 3. Подготовить образ frontend

`frontend/Dockerfile` должен использовать реальные файлы проекта (`vite.config.js`, а не несуществующий `vite.config.ts`):

```dockerfile
FROM node:22-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS production

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
```

Добавьте `frontend/.dockerignore`:

```dockerignore
node_modules
dist
.git
.worktrees
coverage
.env
.env.*
```

### 4. Настроить Nginx в frontend-контейнере

Замените `frontend/nginx.conf` на следующую конфигурацию:

```nginx
server {
  listen 80;
  server_name _;

  root /usr/share/nginx/html;
  index index.html;

  client_max_body_size 100m;

  location / {
    try_files $uri $uri/ /index.html;
  }

  location /assets/ {
    try_files $uri =404;
    expires 1y;
    add_header Cache-Control "public, immutable";
    access_log off;
  }

  location /api/ {
    proxy_pass http://backend:8000;
    proxy_http_version 1.1;

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    proxy_connect_timeout 30s;
    proxy_send_timeout 600s;
    proxy_read_timeout 600s;
    proxy_request_buffering off;
  }

  location = /health {
    proxy_pass http://backend:8000/health;
  }
}
```

`try_files` нужен Vue Router: без него прямое открытие `/projects` или `/profile` завершится ответом 404. [Документация Nginx](https://nginx.org/en/docs/http/ngx_http_core_module.html#try_files). API работает на том же origin, поэтому CORS не требуется, а refresh-cookie с `HttpOnly` и `SameSite=Strict` продолжает работать.

### 5. Закрыть внутренние контейнеры

В корневом `docker-compose.yml`:

- удалите блоки `ports` у `db`, `whisper` и `backend`;
- у `frontend` задайте `127.0.0.1:3000:80` вместо публичного `3000:80`;
- добавьте `restart: unless-stopped` каждому сервису;
- в `backend.environment` задайте `ASPNETCORE_ENVIRONMENT: Production`.

Ключевые части конфигурации:

```yaml
services:
  db:
    restart: unless-stopped
    # ports: удалить

  whisper:
    restart: unless-stopped
    # ports: удалить

  valkey:
    restart: unless-stopped

  backend:
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DATABASE_URL: Host=db;Port=5432;Database=${POSTGRES_DB:-dionysus};Username=${POSTGRES_USER:-dionysus};Password=${POSTGRES_PASSWORD:-dionysus}
      VALKEY_CONNECTION: valkey:6379
      JWT_KEY: ${JWT_KEY}
      AUTH_CODE_PEPPER: ${AUTH_CODE_PEPPER}
      WHISPER_URL: http://whisper:9000
      WHISPER_LANGUAGE: ${WHISPER_LANGUAGE:-ru}
      SMTP_HOST: ${SMTP_HOST}
      SMTP_PORT: ${SMTP_PORT}
      SMTP_USERNAME: ${SMTP_USERNAME}
      SMTP_PASSWORD: ${SMTP_PASSWORD}
    # ports: удалить

  frontend:
    restart: unless-stopped
    ports:
      - "127.0.0.1:3000:80"
```

### 6. Собрать и запустить

Перед первым запуском проверьте frontend:

```sh
cd /opt/dionysus/frontend
npm ci
npm run lint
npm run build
```

Поднимите стек:

```sh
cd /opt/dionysus
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs -f --tail=100
```

Проверьте доступность приложения на loopback-интерфейсе:

```sh
curl -i http://127.0.0.1:3000/health
```

Ожидается `200 OK` и JSON со статусом `ok`.

### 7. Настроить внешний Nginx

Создайте `/etc/nginx/sites-available/dionysus`:

```nginx
server {
  listen 80;
  listen [::]:80;
  server_name app.example.com;

  location / {
    proxy_pass http://127.0.0.1:3000;
    proxy_http_version 1.1;

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }
}
```

Активируйте сайт:

```sh
sudo ln -s /etc/nginx/sites-available/dionysus /etc/nginx/sites-enabled/dionysus
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
curl -i http://app.example.com/health
```

### 8. Выпустить TLS-сертификат

Перед этим DNS должен указывать на сервер, а порт 80 — быть доступен извне.

```sh
sudo certbot --nginx -d app.example.com
sudo certbot renew --dry-run
```

Certbot добавит сертификат и редирект с HTTP на HTTPS. После успешной проверки HTTPS добавьте в TLS-сервер:

```nginx
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header X-Frame-Options "DENY" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
```

Добавляйте HSTS только если HTTPS стабильно работает для домена и всех его поддоменов.

### 9. Проверка и обновление

После развёртывания проверьте:

```sh
curl -I https://app.example.com
curl -i https://app.example.com/health
docker compose ps
docker compose logs --tail=100 backend
docker compose logs --tail=100 frontend
```

В браузере проверьте регистрацию, подтверждение email, логин, обновление страницы, создание проекта файлом до 100 МБ и прямые переходы по маршрутам приложения.

Обновление приложения:

```sh
cd /opt/dionysus
git pull
docker compose up -d --build
docker image prune -f
```

Для пересборки только frontend:

```sh
docker compose build frontend
docker compose up -d --no-deps frontend
```

Docker рекомендует production-override Compose-конфигурации, restart policy и пересоздание контейнеров после пересборки образа. [Руководство Docker Compose для production](https://docs.docker.com/compose/how-tos/production/).

### Резервное копирование PostgreSQL

Данные PostgreSQL хранятся в Docker volume `postgres_data`. Настройте регулярный бэкап вне сервера: без него при потере диска будут потеряны аккаунты, проекты и транскрипции. Пример ручного дампа:

```sh
cd /opt/dionysus
docker compose exec -T db pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" > "dionysus-$(date +%F).sql"
```
