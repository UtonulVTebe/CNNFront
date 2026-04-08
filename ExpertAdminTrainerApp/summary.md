
Ниже — **сводка для ИИ-агента** по репозиторию `CNNFront` (два WPF-клиента + ваше доменное описание), без правок в репозитории.

---

## 1. Продукт и цель

- **Назначение:** десктопные приложения для **тренировки ЕГЭ** с упором на **реалистичность**: поверх **изображений бланков** накладываются **зоны ввода** (ячейки, свободный текст, рисование для математики/физики).
- **КИМы:** отдельные материалы с заданиями; **разметку под них не делают** — только просмотр/скачивание как часть варианта (CNN).
- **Разделение ответственности:**
  - **Клиент:** хранит/использует JSON с **разметкой зон** и при необходимости **`autoAnswers`** — **локальная автопроверка краткого ответа** (нормализация строк/чисел и сравнение с эталоном).
  - **Сервер (по контракту клиентов):** каталог вариантов (CNN), **материалы** (КИМ, критерии, бланки/JSON и пр.), **пользователи**, **заказы на проверку** (`OrderAnswer`), **отзывы эксперта** (`Review`), **транзакции**, загрузка файлов, валидация JSON ответа (`ValidateAnswerPayloadAsync`).

---

## 2. Два приложения в решении

| Проект | Роль |
|--------|------|
| **`ExpertAdminTrainerApp`** | Клиент для **Expert / Admin**: вход, каталог CNN, материалы, **конструктор разметки бланков**, очередь заказов, мои проверки, пользователи/эксперты, инструменты. |
| **`TrainerStudApp`** | Клиент **ученика**: вход, выбор варианта, **сессия экзамена** с заполнением бланков, синхронизация шаблона, автопроверка краткой части, заказы проверки развёрнутой части (по мере реализации в UI). |

Стек: **.NET 8**, **WPF**, **WPF-UI**, **CommunityToolkit.Mvvm**, **DI** (`Microsoft.Extensions.DependencyInjection`), HTTP через **`IHttpClientFactory`** → `ApiClient`, токены в **`ITokenStore`** (файл/память). Конфиг: `appsettings.json` (`Api:BaseUrl`), OpenAPI-копия в `Swagger/swagger.json`.

---

## 3. Модель бланка в JSON (`BlankTemplateDefinition`)

Корень шаблона (см. `ExpertAdminTrainerApp/Domain/BlankTemplateModels.cs` — дубликат домена в `TrainerStudApp/Domain/BlankTemplateModels.cs`):

- **`cnnId`**, **`subject`**, **`option`** — привязка к варианту.
- **`pages`** — список страниц `BlankPageDefinition`: `blankType`, `pageNumber`, `imagePath`, `zones`.
- **`autoAnswers`** — список `AutoAnswerEntry` (`taskId`, `answer`) для **локальной** автопроверки.

Зона **`ZoneDefinition`:**

- Прямоугольник в **нормализованных координатах**: `x`, `y`, `width`, `height`.
- **`fieldName`**, **`fieldType`** (`ZoneFieldType`), **`taskNumber`**.
- Опционально: **`groupId`** (совместное перемещение ряда ячеек), **`fieldRole`** (строковая семантика ЕГЭ: регион, ППЭ, паспорт и т.д.), **`inputMode`** (`Cell` / `Text` / `Drawing`), **`validation`** (`ZoneValidationRules`: длина, regex, маска, только цифры/буквы).

**Типы бланков (`BlankType`):** `Registration`, `AnswerSheet1`, `AnswerSheet2`.

**Типы полей (`ZoneFieldType`):** `Header`, `ShortAnswer`, `LongAnswer`, `FreeForm`, `Correction`, `CellGrid` (бланк №2), `Drawing` (canvas).

Это **согласуется** с вашим описанием: регистрация (шапка + произвольные поля через роли/валидацию), бланк №1 (шапка, краткий ответ, коррекция), бланк №2 (сетка + текст/рисование, своя шапка/номер листа — через отдельные зоны и `fieldRole` при необходимости).

---

## 4. Синхронизация шаблона с сервером

- Сервис **`BlankTemplateSyncService`** (в обоих приложениях по смыслу тот же контракт):
  - Ищет среди материалов CNN запись **`MaterialKind.Blanks`** с заголовком **`"Разметка бланков (JSON)"`**.
  - Загрузка JSON: категория файлов **`blanks`** (`UploadFileCategory`).
- Локальный кэш админки: `%LocalAppData%\ExpertAdminTrainerApp\templates\{cnnId}.json` (`BlankTemplateService`).

---

## 5. Проверка ответов и payload

- **Краткий ответ:** `TrainerStudApp/Services/ShortAnswerAutoGrader.cs` сравнивает ответы по `template.AutoAnswers` с вводом по `taskId` (нормализация пробелов, NBSP, чисел `InvariantCulture` / `ru-RU`).
- **Серверный/обменный формат ответа:** `AnswerPayload` в `Domain/AnswerPayloadModels.cs` — блоки **`Auto_Part`** и **`Exp_Part`**, мета с баллами и датами; развёрнутая часть может быть **Text** или **PDF** (`ExpPartAnswerType`).

---

## 6. API-контракт (глазами `IApiClient`)

Файл `ExpertAdminTrainerApp/Services/IApiClient.cs` задаёт ожидаемые возможности бэкенда:

- **Auth:** логин (токены).
- **CNN:** список, детали, CRUD; **материалы** по виду `MaterialKind` (`Kim`, `Criteria`, `Blanks`, `Other`).
- **Файлы:** `UploadFileAsync`, `DownloadTextAsync` (и у студента — байты для картинок).
- **Заказы:** очередь, «мои», пагинация, claim, обновление, отклонение с причиной.
- **Review:** чтение/создание/обновление проверки с критериями.
- **Пользователи (админ):** список/чтение/обновление, **`ExpertInfo`** (баланс).
- **Инструменты:** `ValidateAnswerPayloadAsync`.

DTO заказов и статусов: `OrderAnswerReadDto`, `OrderAnswerStatus` (`NoCheck`, `PaymentInProgress`, `QueueForCheck`, `Checking`, `Checked`, `RejectedByExpert`), транзакции — `TransactionReadDto` / `PlatformTransactionType`.

Отдельно в DTO есть **`AnnotationZone*`** для разметки полей **на материалах КИМ** (краткий/длинный ответ) — это **параллельная** к бланкам концепция на стороне API; в вашей текущей продуктовой логике **КИМ без разметки**, а разметка — в JSON бланков.

---

## 7. UI-слой (куда смотреть агенту)

**Админ/эксперт:**

- `MainWindow.xaml` + `MainViewModel` — навигация, auth, каталог, заказы, пользователи.
- `Presentation/Views/ConstructorView.*` + `BlankConstructorViewModel` + `ZoneEditorCanvas` — **редактор зон**.
- `Presentation/Controls/BlankDisplayCanvas` — отображение.
- `OrdersView`, `UsersView`, `CatalogView`, `ToolsView`.

**Ученик:**

- `StudentMainViewModel`, `ExamSessionViewModel`, `BlankViewerViewModel`.
- `BlankFillCanvas` / `BlankDisplayCanvas`, `IZoneAnswerSink`.
- В репозитории есть **`TrainerStudApp/summary.md`** — уже частично дублирует эту сводку по студентскому приложению (можно использовать как второй источник).

---

## 8. Чеклист для агента при изменениях

1. Меняя схему JSON шаблона — синхронизировать **`BlankTemplateModels`**, сериализацию в **`BlankTemplateService.TemplateJsonOptions`**, конструктор и студенческий рендер/ввод.
2. Новые семантические поля регистрации/шапки — через **`fieldRole`** и/или **`validation`**, не обязательно новые enum-значения.
3. Публикация шаблона на сервер — только через материал с заголовком **`"Разметка бланков (JSON)"`** и `MaterialKind.Blanks`.
4. Автопроверка — только задачи, перечисленные в **`autoAnswers`**; развёрнутая часть и экспертная проверка — через **заказы/review** и payload, не через локальный JSON эталонов (если явно не расширите модель).

---

Если нужно, в **Agent mode** можно оформить это как отдельный `AGENTS.md` в корне монорепо; в Ask mode я только описал содержание.