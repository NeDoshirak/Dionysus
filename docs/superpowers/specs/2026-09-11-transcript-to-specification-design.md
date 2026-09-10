# Трёхуровневое формирование ТЗ из голосовой записи

## Цель

После загрузки голосовой записи проект автоматически получает одно структурированное ТЗ. Оно состоит из бизнес-контекста и множества отдельных функциональных блоков, сохраняет доказательную связь с сегментами транскрипции и допускает ручное редактирование каждого элемента.

## Границы модели

- `Project` — главный объект для пользователя.
- У проекта ровно одна `VoiceRecording`; исходный звук, исходная транскрипция и результат анализа принадлежат этому проекту.
- У записи ровно один `SpecificationAnalysis`.
- Успешный анализ нельзя запускать повторно. Повтор допустим только для анализа со статусом `failed`.
- Несколько голосовых записей и несколько ТЗ в одном проекте не поддерживаются.

## Сущности

### Исходная транскрипция

`TranscriptSegment` сохраняет исходные `Text`, `StartSeconds` и `EndSeconds` от Whisper. Добавляется nullable-поле `CleanedText`; оно содержит результат нулевого этапа. Исходный текст и таймкоды никогда не изменяются.

### Задача анализа

`SpecificationAnalysis`:

- `Id`, `ProjectId`, `VoiceRecordingId`;
- `Status`: `queued`, `stage0`, `stage1`, `stage2`, `stage3`, `completed`, `failed`;
- `CurrentStage`, `Error`, `StartedAt`, `CompletedAt`, `RetryCount`;
- сохранённые JSON-ответы нулевого, первого и второго этапов для аудита и диагностики.

Фоновый worker забирает задачи `queued`, а retry переводит только `failed`-анализ обратно в `queued`.

### Промежуточный аналитический слой

`AnalysisTopic` хранит нормализованную тему, порядок и принадлежность анализу.

`AnalysisStatement` хранит один атомарный факт, статус `active|superseded|unresolved`, порядок и принадлежность теме.

`AnalysisStatementSegment` связывает statement с одним или несколькими исходными `TranscriptSegment`.

`AnalysisRelation` связывает statements внутри или между темами: `same`, `clarifies`, `supersedes`, `contradicts`, `unresolved`, `related`. Для каждой связи сохраняется краткая причина.

### Финальное ТЗ

`SpecificationBlock` представляет один блок результата: `business_context` либо `function`. У функционального блока есть название, описание, порядок, происхождение `ai|manual` и ссылки на source statements.

`SpecificationItem` — самостоятельный редактируемый элемент блока. Типы: `role`, `functional_requirement`, `user_scenario`, `constraint`, `condition`, `agreement`, `key_question`, `decision_history`, `project_contradiction`. Хранит заголовок, описание, приоритет (`required|desirable|future|unknown` для требований), порядок, происхождение и ссылки на source statements.

Ручной элемент может не иметь source statements и таймкодов. При редактировании AI-элемента его существующие source links сохраняются, пока пользователь явно их не изменит.

Таймкоды в API вычисляются по цепочке `SpecificationItem → AnalysisStatement → TranscriptSegment`; копировать их в финальные элементы не требуется.

## Фоновый конвейер

После успешной транскрипции backend создаёт `SpecificationAnalysis` в статусе `queued`. Отдельный `BackgroundService` обрабатывает задачи по одной записи, исключая одновременную обработку одного анализа. Третий этап параллелит функции через ограниченный семафор с максимум четырьмя запросами Yandex AI одновременно.

### Этап 0: очистка транскрипции

В AI передаются все сегменты с ID и текстом. Роль — редактор транскрипции.

Ответ строго JSON-массив `{ segmentId, cleanedText }`.

Ограничения:

- вернуть каждый существующий segment ID ровно один раз;
- не добавлять, не удалять, не объединять и не менять порядок сегментов;
- исправлять только явные ошибки распознавания, пунктуацию и разрывы фраз;
- не добавлять новые факты и не менять смысл.

Backend валидирует полный набор ID и сохраняет `CleanedText`. Этапы 1–3 используют очищенный текст, но все ссылки ведут к исходным сегментам.

### Этап 1: извлечение фактов

В AI передаются ID, таймкоды и очищенный текст сегментов. Роль — модуль извлечения фактов.

Ответ JSON содержит:

- один блок общего бизнес-контекста;
- темы функций;
- атомарные statements;
- непустые `sourceSegmentIds` только из входного набора.

Модель не создаёт итоговое ТЗ, не удаляет разные варианты решения, не определяет победителя противоречий и не добавляет новые факты.

### Этап 2: ревью структуры

В AI передаётся JSON этапа 1. Роль — строгий reviewer.

Он нормализует только дублирующие темы, сохраняет все statements и их source links, добавляет статусы и relations. Особое внимание — `contradicts`: несовместимые активные statements должны быть явно связаны, включая противоречия между разными функциональными темами. Явно заменённое решение получает `supersedes`, старое statement — `superseded`.

Backend валидирует, что все topic, statement и segment ID относятся к входу; новых facts и source links не допускает.

### Этап 3: формирование ТЗ

Сначала создаётся отдельный `business_context` блок. Затем для каждой нормализованной функциональной темы запускается независимый запрос. Каждый запрос получает общий бизнес-контекст, тему, statements, relations и source links только данной функции.

Роль AI — системный аналитик. Он формирует краткий структурированный блок ТЗ с ролями, функциональными требованиями, сценариями, ограничениями, условиями, договорённостями и `keyQuestions`.

`keyQuestions` — может быть пустым. Он содержит только вопросы, прямо следующие из unresolved statements, missing information или противоречий; это не список общих технических пожеланий.

Каждый AI-сгенерированный финальный элемент обязан содержать хотя бы один существующий source statement ID. Модель может нормализовать и объединять формулировки, но не создавать требование без источника.

После завершения всех функций backend создаёт проектные `project_contradiction` items из relations типа `contradicts`. Они всегда расположены в конце ТЗ, содержат обе стороны противоречия, причину и ссылки на statements/таймкоды.

## JSON-контракты AI-конвейера

Все контракты используют `schemaVersion: "1.0"`. AI-ответ обязан быть единственным валидным JSON-объектом без Markdown и дополнительных полей. Backend десериализует ответы со строгой схемой: неизвестные поля, `null` в обязательных полях, невалидные enum-значения и нарушенные ID переводят анализ в `failed`.

### Общие типы

```json
{
  "segment": {
    "id": "<guid TranscriptSegment>",
    "startSeconds": 12.4,
    "endSeconds": 18.9,
    "text": "Текст сегмента"
  },
  "source": {
    "sourceSegmentIds": ["<guid TranscriptSegment>"]
  }
}
```

`sourceSegmentIds` всегда непустой массив уникальных существующих segment IDs. Они принадлежат только текущей `VoiceRecording`. Backend не доверяет тексту и таймкодам, которые могут вернуть модели: таймкоды восстанавливаются только из БД по IDs.

### Контракт этапа 0 — очистка транскрипции

**Вход `Stage0CleanupRequest`:**

```json
{
  "schemaVersion": "1.0",
  "segments": [
    {
      "id": "2e9a1111-1111-1111-1111-111111111111",
      "startSeconds": 0.0,
      "endSeconds": 4.2,
      "text": "пользаватель дажен зайти по карпаративнай учётки"
    }
  ]
}
```

**Выход `Stage0CleanupResponse`:**

```json
{
  "schemaVersion": "1.0",
  "segments": [
    {
      "segmentId": "2e9a1111-1111-1111-1111-111111111111",
      "cleanedText": "Пользователь должен войти по корпоративной учётной записи."
    }
  ]
}
```

Инварианты: выходной набор `segmentId` в точности равен входному набору; каждый ID встречается один раз; `cleanedText` непустой. Модель не получает права менять IDs, временные границы, порядок или количество сегментов.

### Контракт этапа 1 — извлечение контекста, тем и statements

**Вход `Stage1ExtractionRequest`:**

```json
{
  "schemaVersion": "1.0",
  "segments": [
    {
      "id": "2e9a1111-1111-1111-1111-111111111111",
      "startSeconds": 0.0,
      "endSeconds": 4.2,
      "text": "Пользователь должен войти по корпоративной учётной записи."
    }
  ]
}
```

Здесь `text` равен `CleanedText`, если он есть, иначе исходному `Text` Whisper.

**Выход `Stage1ExtractionResponse`:**

```json
{
  "schemaVersion": "1.0",
  "businessContext": [
    {
      "id": "ctx-1",
      "text": "Система используется сотрудниками компании.",
      "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
    }
  ],
  "topics": [
    {
      "id": "topic-1",
      "name": "Авторизация",
      "statements": [
        {
          "id": "st-1",
          "text": "Пользователь должен иметь возможность войти с корпоративной учётной записью.",
          "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
        },
        {
          "id": "st-2",
          "text": "Корпоративная учётная запись используется для входа в систему.",
          "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
        }
      ]
    }
  ]
}
```

Допустимые форматы ID: `ctx-N`, `topic-N`, `st-N`, где `N` — положительное целое. IDs topics и statements уникальны в пределах ответа. В `businessContext` и `statements` запрещены пустые `sourceSegmentIds`. Этап не возвращает статусы, relations или финальные требования.

### Контракт этапа 2 — ревью, дедупликация и противоречия

**Вход `Stage2ReviewRequest`:** в точности валидный `Stage1ExtractionResponse`.

**Выход `Stage2ReviewResponse`:**

```json
{
  "schemaVersion": "1.0",
  "businessContext": [
    {
      "id": "ctx-1",
      "text": "Система используется сотрудниками компании.",
      "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
    }
  ],
  "topics": [
    {
      "id": "topic-1",
      "name": "Авторизация",
      "statementIds": ["st-1", "st-2"]
    }
  ],
  "statements": [
    {
      "id": "st-1",
      "text": "Пользователь должен иметь возможность войти с корпоративной учётной записью.",
      "status": "active",
      "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
    },
    {
      "id": "st-2",
      "text": "Корпоративная учётная запись используется для входа в систему.",
      "status": "active",
      "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
    }
  ],
  "relations": [
    {
      "id": "rel-1",
      "type": "clarifies",
      "sourceStatementIds": ["st-1"],
      "targetStatementIds": ["st-2"],
      "reason": "Второе утверждение уточняет способ входа."
    }
  ]
}
```

`status` допускает только `active`, `superseded`, `unresolved`. `type` допускает только `same`, `clarifies`, `supersedes`, `contradicts`, `unresolved`, `related`.

Валидатор требует, чтобы набор statements в ответе совпадал со входом по IDs, `text` и `sourceSegmentIds`; ни один statement не может исчезнуть или получить новые источники. Каждое `statementId` присутствует ровно в одной нормализованной теме. Topic IDs допускается переименовывать и объединять, но нельзя удалять statements. Все IDs relation должны ссылаться на существующие statements; `sourceStatementIds` и `targetStatementIds` непусты, уникальны и не пересекаются. Для `supersedes` старые statements обязаны иметь `superseded`, новые — `active`; для `contradicts` обе стороны остаются `active`.

### Контракт этапа 3 — один функциональный блок ТЗ

Для каждой темы backend формирует самостоятельный запрос. События business context передаются как общая информация, но не подменяют источники функциональных требований.

**Вход `Stage3FunctionRequest`:**

```json
{
  "schemaVersion": "1.0",
  "businessContext": [
    {
      "id": "ctx-1",
      "text": "Система используется сотрудниками компании.",
      "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
    }
  ],
  "function": {
    "topicId": "topic-1",
    "name": "Авторизация",
    "statements": [
      {
        "id": "st-1",
        "text": "Пользователь должен иметь возможность войти с корпоративной учётной записью.",
        "status": "active",
        "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
      },
      {
        "id": "st-2",
        "text": "Корпоративная учётная запись используется для входа в систему.",
        "status": "active",
        "sourceSegmentIds": ["2e9a1111-1111-1111-1111-111111111111"]
      }
    ],
    "relations": [
      {
        "id": "rel-1",
        "type": "clarifies",
        "sourceStatementIds": ["st-1"],
        "targetStatementIds": ["st-2"],
        "reason": "Второе утверждение уточняет способ входа."
      }
    ]
  }
}
```

**Выход `Stage3FunctionResponse`:**

```json
{
  "schemaVersion": "1.0",
  "function": {
    "title": "Авторизация",
    "description": "Вход сотрудников в систему.",
    "sourceStatementIds": ["st-1"]
  },
  "roles": [],
  "functionalRequirements": [
    {
      "id": "req-1",
      "title": "Вход с корпоративной учётной записью",
      "description": "Пользователь должен иметь возможность войти в систему с корпоративной учётной записью.",
      "priority": "required",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "userScenarios": [],
  "constraints": [],
  "conditions": [],
  "agreements": [],
  "keyQuestions": []
}
```

Все массивы обязательны и используют `[]`, если данных нет. Допустимые priority: `required`, `desirable`, `future`, `unknown`. Все AI-элементы, включая function, обязаны иметь непустой массив существующих `sourceStatementIds` только из входной функции. Форматы локальных IDs: `role-N`, `req-N`, `scenario-N`, `constraint-N`, `condition-N`, `agreement-N`, `question-N`.

`keyQuestions` имеет поля `id`, `title`, `description`, `reason` и `sourceStatementIds`; `reason` допускает `contradiction`, `unresolved`, `missing_information`. Массив может быть пустым. Backend формирует `project_contradiction` items самостоятельно из `Stage2ReviewResponse.relations`, а не поручает модели повторно выводить противоречия на этапе 3.

Точные формы остальных массивов `Stage3FunctionResponse`:

```json
{
  "roles": [
    {
      "id": "role-1",
      "name": "Сотрудник",
      "description": "Пользователь системы.",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "userScenarios": [
    {
      "id": "scenario-1",
      "title": "Вход в систему",
      "actor": "Сотрудник",
      "description": "Сотрудник входит с корпоративной учётной записью.",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "constraints": [
    {
      "id": "constraint-1",
      "description": "Ограничение, указанное в разговоре.",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "conditions": [
    {
      "id": "condition-1",
      "description": "Условие выполнения требования.",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "agreements": [
    {
      "id": "agreement-1",
      "description": "Явно достигнутая договорённость.",
      "sourceStatementIds": ["st-1"]
    }
  ],
  "keyQuestions": [
    {
      "id": "question-1",
      "title": "Вопрос по функции",
      "description": "Что требуется уточнить.",
      "reason": "unresolved",
      "sourceStatementIds": ["st-1"]
    }
  ]
}
```

Элементы каждого массива используют только указанные поля. `roles`, `userScenarios`, `constraints`, `conditions`, `agreements` и `keyQuestions` не принимают `priority`; это поле допустимо только у `functionalRequirements`.

## Ошибки и повтор

Если Yandex AI возвращает сетевую ошибку, невалидный JSON, неизвестный ID, недопустимый статус или нарушает правила этапа, worker переводит анализ в `failed`, сохраняет безопасную диагностическую ошибку и не публикует частично сформированное финальное ТЗ.

`POST .../retry` разрешён только владельцу проекта при `failed`. Он очищает только неполные промежуточные результаты, сохраняет оригинальную транскрипцию и запускает анализ заново. Успешное ТЗ не перезаписывается.

## API

Все маршруты требуют JWT и проверяют владельца проекта.

- `GET /api/projects/{projectId}/specification` — статус анализа, бизнес-контекст, функции, элементы, source statements, сегменты и вычисленные таймкоды.
- `POST /api/projects/{projectId}/specification/retry` — ставит failed-анализ обратно в очередь.
- `GET /api/projects/{projectId}/specification/functions/{functionId}` — детальный функциональный блок.
- `POST /api/projects/{projectId}/specification/functions` — ручное создание блока.
- `PATCH /api/projects/{projectId}/specification/functions/{functionId}` — обновление названия/описания/порядка.
- `DELETE /api/projects/{projectId}/specification/functions/{functionId}` — удаление функции и её items.
- `POST /api/projects/{projectId}/specification/functions/{functionId}/items` — ручное создание элемента.
- `PATCH /api/projects/{projectId}/specification/functions/{functionId}/items/{itemId}` — изменение элемента.
- `DELETE /api/projects/{projectId}/specification/functions/{functionId}/items/{itemId}` — удаление элемента.

## Проверки

- Нулевой этап отклоняет отсутствующие, дублирующиеся и неизвестные segment IDs.
- Этап 1 сохраняет только sourceSegmentIds из записи и не создаёт финальное ТЗ.
- Этап 2 сохраняет все statements и корректно фиксирует `supersedes` и `contradicts`.
- Этап 3 отправляет один запрос на функцию, ограничивает параллелизм четырьмя запросами, сохраняет раздельные items и key questions.
- Противоречия из второго этапа становятся проектными items в конце ТЗ.
- Таймкоды каждого AI-элемента восстанавливаются из связанных сегментов.
- Владелец может редактировать, добавлять и удалять блоки/items; другой пользователь получает `404`.
- Успешный анализ нельзя перезапустить; failed-анализ можно retry.
- Миграции применяются на пустой и уже существующей PostgreSQL базе; unit и integration tests, backend build, Docker build, Compose config и frontend build проходят.
