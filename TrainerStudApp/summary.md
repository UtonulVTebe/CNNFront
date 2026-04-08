
Ниже — **сводка для ИИ-агента** по репозиторию `TrainerStudApp` с учётом вашего доменного описания и того, что реально заложено в коде.

---

## Назначение продукта

**Desktop-клиент (WPF)** для тренировки ЕГЭ с акцентом на **реалистичный ввод**: поверх сканов/изображений бланков рисуются **зоны ввода** (ячейки, текст, рисование).  
**КИМ** (условные задания) в коде **не размечаются** — это отдельные материалы для просмотра; разметка относится к **бланкам**.

**Целевая архитектура (ваше видение):** в JSON шаблоне хранятся и **разметка**, и **эталоны для краткого ответа** — **автопроверка на клиенте**. Сервер — **КИМы/обновления**, **пользователи**, **транзакции**, **связь ученик ↔ эксперт** (заказ проверки развёрнутой части). Клиент вызывает **Auth**, **CNN**, **OrderAnswers** (`/api/orderanswers`) и скачивание материалов; оплата и прочие платежи — вне текущего UI.

---

## Стек и структура

| Аспект | Детали |
|--------|--------|
| Платформа | **.NET 8**, **WPF** (`TrainerStudApp.csproj`) |
| UI | **WPF-UI** (`FluentWindow`, карточки), MVVM через **CommunityToolkit.Mvvm** |
| DI | `Microsoft.Extensions.DependencyInjection` в `App.xaml.cs` |
| HTTP | `IHttpClientFactory` → `ApiClient` + **Bearer**; токены в `ITokenStore` (файловое хранилище по умолчанию) |
| Конфиг | `appsettings.json` (`Api:BaseUrl`), копия OpenAPI в `Swagger/swagger.json` |

**Основные папки:** `Domain/` (модели, enum’ы, DTO), `Services/` (API, шаблоны, автопроверка, медиа), `Presentation/ViewModels/`, `Presentation/Controls/` (канвы бланка).

---

## Модель бланка в JSON (`BlankTemplateDefinition`)

Корень шаблона привязан к варианту: `cnnId`, `subject`, `option`, список страниц `pages`, плюс **`autoAnswers`** — эталоны по `taskId` для локальной автопроверки.

Страница (`BlankPageDefinition`): `blankType`, `pageNumber`, `imagePath`, `zones`.

Зона (`ZoneDefinition`): прямоугольник в **нормализованных координатах** (`x`, `y`, `width`, `height`), `fieldName`, `fieldType`, `taskNumber`, опционально `groupId`, **`fieldRole`** (семантика ЕГЭ: регион, ППЭ, паспорт и т.д.), **`inputMode`** (ячейка / текст / рисование), **`validation`** (`ZoneValidationRules`: длина, regex, маска, только цифры/буквы).

Типы бланков и полей зафиксированы в enum’ах:

```35:64:c:\Users\Maksim\Documents\GitHub\CNNFront\TrainerStudApp\Domain\Enums.cs
public enum BlankType
{
    Registration = 0,
    AnswerSheet1 = 1,
    AnswerSheet2 = 2
}

public enum ZoneFieldType
{
    Header = 0,
    ShortAnswer = 1,
    LongAnswer = 2,
    FreeForm = 3,
    Correction = 4,
    /// <summary>Сетка ячеек (бланк №2 и т.п.).</summary>
    CellGrid = 5,
    /// <summary>Зона для рукописного/графического ответа (canvas на клиенте).</summary>
    Drawing = 6
}

/// <summary>Способ ввода в зоне на клиенте проверки.</summary>
public enum ZoneInputMode
{
    /// <summary>По ячейкам (по умолчанию для старых шаблонов).</summary>
    Cell = 0,
    /// <summary>Свободный текст.</summary>
    Text = 1,
    /// <summary>Рисование (математика/физика).</summary>
    Drawing = 2
}
```

Это **совпадает** с вашим описанием: бланк №1 (краткий ответ, коррекция, шапка), №2 (**CellGrid** и/или **Drawing**/текст), регистрация (**Header**, **FreeForm** и т.д. через роли и валидацию).

---

## Поведение клиента (важное для агента)

1. **Синхронизация шаблона с сервера:** `BlankTemplateSyncService` ищет материал CNN с `MaterialKind.Blanks` и заголовком **`"Разметка бланков (JSON)"`**, качает JSON, при необходимости выравнивает `cnnId`. Локальный кэш — `%LocalAppData%\TrainerStudApp\templates\{cnnId}.json` (`BlankTemplateService`).

2. **Картинки страниц:** `TemplateMediaResolver` склеивает относительный `imagePath` с каталогом URL JSON-материала, чтобы получить полный URL; `ExamSessionViewModel` грузит байты через `IApiClient.DownloadBytesAsync` с авторизацией.

3. **Сеанс экзамена:** `ExamTemplatePageCloner.CreateExamWorkingCopy` — порядок страниц **Регистрация → №1 → №2**, новые `Id` зон, копия эталонов `AutoAnswers`. Хранится прототип последнего листа №2 для **одного дополнительного** бланка №2 (`AddAnswerSheet2`).

4. **Ввод:** `BlankFillCanvas` (наследник `BlankDisplayCanvas`) строит слой ввода: для интерактивных зон — сетка ячеек, `TextBox`, или **Ink** (рисование) в зависимости от `FieldType` + `InputMode`.

5. **Автопроверка:** `ExamSessionViewModel.BuildAnswersByTaskNumber` собирает ответы только из зон **`ShortAnswer`** и **`Correction`** (по ключам зоны/страницы), сортирует по позиции; `ShortAnswerAutoGrader` сравнивает с `template.AutoAnswers` с нормализацией пробелов и чисел (invariant / ru-RU). Без `autoAnswers` кнопка завершения показывает сообщение, что эталонов нет.

6. **Payload для развёрнутой части / эксперта:** модель `AnswerPayload` (`Auto_Part`, `Exp_Part`, мета с баллами) описана в `AnswerPayloadModels.cs` — удобна для сериализации результатов; прямой отправки на сервер в просмотренном `IApiClient` нет.

---

## API и домен сервера (в коде клиента)

`IApiClient` реализует только: **логин**, **список CNN**, **детали CNN**, **скачивание текста/байтов** по URL с Bearer и refresh при 401.

В `Dtos.cs` заранее описаны сущности под бэкенд: пользователи, эксперты, материалы CNN (`MaterialKind`: Kim, Criteria, Blanks, Other), **зоны аннотаций на материалах** (`AnnotationZone*`), **заказы проверки** (`OrderAnswer*`, `OrderAnswerStatus`), **отзывы эксперта** (`Review*`), **транзакции** (`TransactionReadDto`, `PlatformTransactionType`), загрузка файлов. Это **контракт/задел** под вашу схему «сервер = пользователи, КИМы, деньги, эксперты», а не полный набор вызовов в текущем UI.

---

## Соответствие вашему тексту про поля бланков

| Ваше описание | Где в коде |
|---------------|------------|
| Регистрация: регион, ОО, класс, ППЭ, аудитория, предмет, дата, ФИО, паспорт-заглушка | Зоны с `fieldRole` / `Header` / `FreeForm` + `ZoneValidationRules` (маски, длины) |
| Бланк №1: 2+2+3 буквы шапка, краткие ответы, замена ошибочных | `AnswerSheet1`, `ShortAnswer`, `Correction`, `Header` |
| Бланк №2: клетки или canvas, та же шапка, номер листа | `AnswerSheet2`, `CellGrid`, `Drawing` / `Text`, дублирование страниц №2 |
| КИМ без разметки | `MaterialKind.Kim` в каталоге; превью в `StudentMainViewModel` |
| JSON = разметка + правильные ответы, проверка на клиенте | `BlankTemplateDefinition` + `AutoAnswers` + `ShortAnswerAutoGrader` |

---

## Файлы-якоря для навигации агента

- Шаблон и зоны: `Domain/BlankTemplateModels.cs`, `Domain/ZoneValidationModels.cs`, `Domain/Enums.cs`
- Клонирование сеанса и доп. лист №2: `Domain/ExamTemplatePageCloner.cs`
- Синхронизация/кэш: `Services/BlankTemplateSyncService.cs`, `Services/BlankTemplateService.cs`
- Автопроверка: `Services/ShortAnswerAutoGrader.cs`, `Presentation/ViewModels/ExamSessionViewModel.cs`, `Presentation/ViewModels/ExamAnswerKeyHelper.cs`
- Отрисовка и ввод: `Presentation/Controls/BlankDisplayCanvas.cs`, `Presentation/Controls/BlankFillCanvas.cs`, `Presentation/Controls/IZoneAnswerSink.cs`
- Главный сценарий ученика: `Presentation/ViewModels/StudentMainViewModel.cs`, `MainWindow.xaml`
- Точка входа и DI: `App.xaml.cs`
- Контракт API: `Services/IApiClient.cs`, `Services/ApiClient.cs`, `Services/HttpApiException.cs`, `Domain/Dtos.cs`
- Заказы на проверку: `Presentation/ViewModels/StudentOrdersViewModel.cs`, конвертер статуса `Presentation/Converters/OrderAnswerStatusConverter.cs`

---

## Заказы на проверку (OrderAnswer) — API и UI

**Префикс:** `api/orderanswers`. Ошибки с телом `{ "message": "..." }` мапятся в `HttpApiException` (наследник `InvalidOperationException`) с кодом HTTP.

**Методы клиента (`IApiClient`):** `CreateOrderAnswerAsync`, `GetMyOrderAnswersMineAsync`, `GetMyOrderAnswersPageAsync` (query `OrderAnswerListQuery`), `GetOrderAnswerByIdAsync`, `UpdateOrderAnswerAsync`, `GetOrderAnswerReviewAsync` (404 — отзыва ещё нет).

**DTO:** `OrderAnswerCreateDto`, `OrderAnswerReadDto`, `OrderAnswerUpdateDto`, `OrderAnswerListQuery`, `ReviewReadDto` / `ReviewCriterionDto`. Статус — `OrderAnswerStatus` (0…5).

### Краткое описание экранов (раздел «Проверки», боковая навигация)

| Экран / состояние | Поведение |
|-------------------|-----------|
| **Список заказов** | Слева список всех своих заказов (`GET …/mine`), сортировка с сервера по `changedAt` убыв. При открытии вкладки — автообновление. |
| **Карточка заказа** | Справа: статус (человекочитаемо), id, CNN, поле `answerUrl`, кнопки «Сохранить ссылку» и «В очередь на проверку» (статус 2), если разрешено клиентской логикой. |
| **Создание** | Панель «Новый заказ»: выбор варианта из каталога CNN, URL файла, чекбокс «сразу в очередь» (опциональный `status` в POST). Загрузка файла — отдельно (Files API), сюда только URL. |
| **В очереди / оплата / проверка** | Отображаются как статусы «В очереди», «Ожидание оплаты», «Проверяется»; платёжные вызовы не делаются. |
| **Отклонено** | Показ `rejectionReason`; можно править URL и снова отправить в очередь. |
| **Проверено** | Блок «Результат проверки»: `GET …/{id}/review` — таблица критериев, общий комментарий, сумма баллов; если 404 — текст, что отзыва пока нет. |

**Навигация приложения:** узкая **колонка слева (~208px)** со списком разделов (`ListBox`), справа — контент выбранного раздела (`SelectedNavIndex` + `IndexMatchToVisibilityConverter`). Экран входа для гостя — **центрированная карточка** в разделе «Профиль».

---

**Краткий вывод для агента:** это **WPF-клиент ученика**, который авторизуется, работает с **каталогом CNN**, **заказами на проверку развёрнутой части**, подгружает **JSON разметки бланков** и изображения страниц, заполняет бланк и **локально** сверяет краткие ответы с `autoAnswers`.