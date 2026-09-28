# Сборка, тесты и пакет Thunderstore

## Требования

- Windows с Windows PowerShell 5.1, .NET SDK 8 или новее, .NET Framework 4.8 для
  запуска net48-тестов. SDK — только компилятор: плагин нацелен на **net48**, C# 7.3.
- Из игры: `valheim_Data/Managed/UnityEngine.dll`, `UnityEngine.CoreModule.dll`;
  тестам дополнительно нужен `UnityEngine.AudioModule.dll` (только для компиляции).
- Из профиля BepInEx 5: `BepInEx.dll`, `0Harmony.dll` и `Newtonsoft.Json.dll` 13.x
  (JsonDotNET). Ссылки определены один раз в `build/RuntimeReferences.props`.

Зафиксированные зависимости разработки: Microsoft.NETFramework.ReferenceAssemblies
1.0.3, NUnit 3.14.0, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.11.1 и
Thunderstore CLI (`tcli`) 0.2.4 как локальный dotnet-tool (`.config/dotnet-tools.json`).
Источник пакетов задан в `nuget.config`, чтобы сборка не зависела от глобальных
настроек NuGet. Эти пакеты в мод не попадают.

Для выпуска дополнительно нужны Python 3.11+, [uv](https://docs.astral.sh/uv/) и
GitHub CLI 2.98+ с авторизацией; release-kit закреплён в `.github/relkit.pyz` (0.31.0).

## Стандартный путь

```powershell
.\Build.ps1 -GamePath 'D:\Games\Valheim' -ProfilePath 'D:\Profiles\Valheim'
```

Пути примерные. Можно запустить `Build.cmd` без аргументов и ответить на два запроса;
`ProfilePath` допускает и саму папку `BepInEx`. При нетипичном размещении зависимостей
передайте `-BepInExCorePath` и `-NewtonsoftJsonPath`. Если в профиле несколько
Newtonsoft.Json 13.x, скрипт требует явный путь и не выбирает первый попавшийся.

Не переданные параметры берутся из переменных окружения `TOLMACH_GAME_PATH`,
`TOLMACH_PROFILE_PATH`, `TOLMACH_BEPINEX_CORE_PATH` и `TOLMACH_NEWTONSOFT_JSON_PATH`.
Интерактивный запрос остаётся последним вариантом; в `powershell -NonInteractive`
вместо него сборка сразу останавливается с именем недостающего пути.

`-OutputDirectory <папка>` кладёт пакет и `SHA256SUMS` с его хешем в указанную папку
вместо `artifacts/`. Папка не должна существовать заранее. Этот режим использует выпуск
через release-kit (см. ниже).
Пути с `;`, `,`, `%` или переносом строки отклоняются, чтобы MSBuild не принял их за
разделители свойств; для таких мест используйте junction с обычным именем.

`Build.ps1` выполняет по порядку:

1. `dotnet test` тестового проекта. Он собирает production-проект по `ProjectReference`
   и запускает NUnit с настоящей Harmony из профиля. Пустое обнаружение тестов,
   пропуски, ошибки компиляции и падения останавливают сборку.
2. Сверку DLL рядом с тестами с выходом production-проекта, версии сборки с `<Version>`
   в csproj и всех 37 каталогов с их тестовыми копиями.
3. `build-receipt.json`: версия SDK, SHA-256 DLL, число тестов, хеши ссылок компилятора
   и каталогов. Локальные пути в него не пишутся.
4. `tcli build` по `thunderstore.toml` с версией из csproj: манифест, иконка, README,
   CHANGELOG, LICENSE, `plugins/Tolmach.dll`, `plugins/catalog/*.json`.
5. Проверку готового ZIP (см. ниже) и перенос его в `artifacts/`.

Результат: `artifacts/Muratovnik-Tolmach-<версия>.zip`. Журналы, TRX и
receipt — в `artifacts/run-<GUID>/`. Прежний пакет той же версии переносится в
`artifacts/previous/`, чтобы старый успех не приняли за новый результат. Игра и
профиль при сборке не изменяются.

## Что проверяется в готовом пакете

Правила Thunderstore: в корне `manifest.json`, `icon.png` и `README.md`; иконка — PNG
256×256; манифест в UTF-8 без BOM; `name` из `[A-Za-z0-9_]`, `version_number` вида
`Major.Minor.Patch` совпадает с версией сборки, `description` не длиннее 250 символов,
`website_url` присутствует, зависимости имеют вид `Команда-Пакет-Версия`.

Раскладка для Gale: всё, что загружает плагин, лежит под `plugins/`. Установщик Gale
кладёт содержимое `plugins/` в `BepInEx/plugins/<пакет>/` с сохранением подпапок, а
файлы вне распознаваемых папок раскладывает плоско; `catalog/` в корне архива
потерял бы структуру, и плагин не нашёл бы каталоги. Лишние и недостающие записи,
обратные слеши и дубликаты отклоняются. DLL и каталоги внутри ZIP сверяются по SHA-256
с протестированными файлами, receipt — с упакованной DLL.

После правок этой проверки запустите её контроли:
`powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-PackageCheck.ps1`.
Скрипт берёт функции прямо из `Build.ps1`: собранный пакет должен приниматься, а шесть
намеренно испорченных копий (каталоги вне `plugins/`, BOM в манифесте, иконка 128×128,
длинное описание, чужая DLL, нет README.md) — отклоняться.

## Установка

Готовый ZIP берётся из `artifacts/` после сборки или со страницы
[релизов GitHub](https://github.com/Muratovnik/Tolmach/releases).

- Gale: «Импорт» → «…локальный мод» и выбрать ZIP, или перетащить ZIP в окно Gale.
  Из проекта `valheim` то же делает `node gale.mjs install-local '<ZIP>'` с резервной
  копией профиля.
- r2modman: Settings → Import local mod.
- Вручную: содержимое `plugins/` из ZIP скопировать в
  `BepInEx/plugins/Tolmach/`.

Менеджер модов устанавливает зависимости из манифеста, раскладывает файлы и при
повторном импорте заменяет прежнюю версию. Отдельного установщика в проекте больше нет.

## Ручные команды

Только тесты, без упаковки:

```powershell
dotnet test .\tests\Tolmach.Tests.csproj -c Release `
  '-p:GamePath=D:\Games\Valheim' '-p:ProfilePath=D:\Profiles\Valheim' `
  '-p:NewtonsoftJsonPath=D:\Profiles\Valheim\BepInEx\plugins\JsonDotNET\Newtonsoft.Json.dll'
```

`tcli build` без `Build.ps1` запускать не стоит: версия пакета передаётся скриптом, а
`artifacts/stage` с протестированными файлами существует только во время сборки.

## Выпуск через release-kit

Порядок выпуска для сопровождающего — в [README.md](../README.md#выпуск-новой-версии).
Что при этом выполняет `release run … --prepare` по [relkit.toml](../relkit.toml):

1. **checks** в рабочей копии: `tools/validate.py` и pytest-контроли валидатора.
   pytest 9.1.1 запускается через `uv` в отдельном окружении Python 3.11, поэтому
   глобально его ставить не нужно.
2. Аудит публикации: секреты (Betterleaks), локальные ссылки в Markdown (Lychee),
   правила из `[exposure]`. Сначала по рабочему дереву, затем по всей истории Git.
3. **build** в снимке закоммиченного дерева: `Build.ps1 -OutputDirectory {assets}`.
   Пути к игре и профилю берутся из `TOLMACH_GAME_PATH` и `TOLMACH_PROFILE_PATH`;
   незакоммиченные файлы и `artifacts/` рабочей копии в снимок не попадают.
4. **smoke**: `tools/Test-PackageCheck.ps1` на готовом ZIP — пакет принимается, шесть
   испорченных копий отклоняются.
5. Создание тега `v<версия>`, отправка тега и ветки `main`, черновик релиза GitHub с ZIP
   и `SHA256SUMS`, сверка загруженных файлов с подготовленными и публикация. Текст
   релиза — запись этой версии из `CHANGELOG.md`.

Квитанции и журналы попыток хранятся в `.git/relkit/`. Релизы в репозитории неизменяемые
(GitHub immutable releases): опубликованный релиз нельзя перезаписать, исправление
выходит новой версией.

## Проверка данных

Python 3.10+; для pytest-контролей нужен pytest, для сверки YAML с исходным архивом —
PyYAML:

```text
python tools/validate.py [--evidence /path/to/unpacked/valheim-handoff]
python -m pytest tests/test_validation.py -q
```

Без `--evidence` сверка с исходным архивом помечается пропущенной. Python не исполняет
и не имитирует C#-переводчик: поведение проверяют NUnit-тесты.
