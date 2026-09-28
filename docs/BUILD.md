# Сборка, проверки и выпуск

Руководство для сопровождающего: как собрать пакет из исходников, проверить правку
переводов и выпустить версию на GitHub. Установка готового пакета описана в
[README](../README.md#установка).

## Требования

- Windows с Windows PowerShell 5.1, .NET SDK 8 или новее и .NET Framework 4.8 для
  запуска тестов. SDK служит только компилятором: плагин нацелен на **net48**, C# 7.3.
- Установленная Valheim: `valheim_Data/Managed/UnityEngine.dll` и
  `UnityEngine.CoreModule.dll`; тестам нужен ещё `UnityEngine.AudioModule.dll`, только
  для компиляции.
- Профиль менеджера модов с BepInEx 5 и JsonDotNET: `BepInEx.dll`, `0Harmony.dll` и
  `Newtonsoft.Json.dll` 13.x. Ссылки на эти файлы заданы в `build/RuntimeReferences.props`.
- Python 3.10+ для проверки данных.
- Для выпуска: Python 3.11+, [uv](https://docs.astral.sh/uv/) и GitHub CLI 2.98+ с
  правом записи в репозиторий. release-kit 0.31.0 закреплён в `.github/relkit.pyz`.

Зависимости разработки зафиксированы: Microsoft.NETFramework.ReferenceAssemblies 1.0.3,
NUnit 3.14.0, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.11.1 и Thunderstore
CLI (`tcli`) 0.2.4 как локальный dotnet-tool (`.config/dotnet-tools.json`). При первой
сборке NuGet скачивает их из источника, заданного в `nuget.config`, поэтому глобальные
настройки NuGet не нужны. В пакет эти зависимости не попадают.

## Сборка пакета

Выполните в корне репозитория, подставив свои пути:

```powershell
.\Build.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Valheim' `
  -ProfilePath "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default"
```

`GamePath` — папка установки Valheim; в примере это стандартная папка Steam.
`ProfilePath` — папка профиля менеджера модов (в примере профиль `Default` в Gale) или
сама папка `BepInEx` в нём. `Build.cmd` без аргументов запрашивает пути, которые не
заданы переменными окружения (см. ниже).

Сборка ничего не пишет ни в игру, ни в профиль, поэтому игру закрывать не нужно.
Результат — `artifacts/Muratovnik-Tolmach-<версия>.zip`. Журналы, TRX и
`build-receipt.json` этого запуска лежат в `artifacts/run-<GUID>/`. Прежний пакет той же
версии переносится в `artifacts/previous/`, чтобы старый результат не приняли за новый.

### Пути и параметры

Чтобы не передавать пути каждый раз, задайте переменные окружения `TOLMACH_GAME_PATH` и
`TOLMACH_PROFILE_PATH`. Их же использует выпуск через release-kit. Параметры, если они
переданы, важнее переменных. Запрос пути в консоли — последний вариант: в
`powershell -NonInteractive` сборка вместо него сразу останавливается и называет
недостающий путь.

- `-BepInExCorePath` и `-NewtonsoftJsonPath` (переменные `TOLMACH_BEPINEX_CORE_PATH` и
  `TOLMACH_NEWTONSOFT_JSON_PATH`) нужны при нетипичном размещении этих файлов. Если в
  профиле несколько Newtonsoft.Json 13.x, скрипт требует явный путь и не выбирает
  первый попавшийся.
- `-OutputDirectory <папка>` кладёт пакет и `SHA256SUMS` с его хешем в указанную папку
  вместо `artifacts/`. Папка не должна существовать заранее. Этот режим использует
  release-kit.

Пути с `;`, `,`, `%` или переносом строки отклоняются, чтобы MSBuild не принял их за
разделители свойств. Для таких мест создайте junction с обычным именем.

### Что делает Build.ps1

1. `dotnet test` тестового проекта. Он собирает плагин по `ProjectReference` и запускает
   NUnit с настоящей Harmony из профиля. Ошибка компиляции, падение, пропуск теста или
   пустое обнаружение тестов останавливают сборку.
2. Сверяет DLL рядом с тестами с выходом сборки плагина, версию сборки — с `<Version>`
   в `Tolmach.csproj`, все 37 каталогов — с их тестовыми копиями.
3. Пишет `build-receipt.json`: версия SDK, SHA-256 DLL, число тестов, хеши ссылок
   компилятора и каталогов. Локальные пути в него не попадают.
4. Собирает ZIP через `tcli build` по `thunderstore.toml` с версией из csproj: манифест,
   иконка, README, CHANGELOG, LICENSE, `plugins/Tolmach.dll`, `plugins/catalog/*.json`.
5. Проверяет готовый ZIP (см. [Проверка готового пакета](#проверка-готового-пакета)) и
   переносит его в `artifacts/` или в `-OutputDirectory`.

`tcli build` отдельно от `Build.ps1` запускать не стоит: версию пакета передаёт скрипт,
а `artifacts/stage` с протестированными файлами существует только во время сборки.

## Изменение переводов

Каждый мод описан файлом в [catalog/](../catalog/): идентичность (GUID, имя сборки,
версия плагина, namespaces), словарные ключи `words`/`englishWords`, точные строки
`texts`, шаблоны `patterns`, адресные литералы `literals` и другие адаптеры. Формат и
политика переводов — в [ARCHITECTURE.md](ARCHITECTURE.md), покрытие по модам — в
[COVERAGE.md](COVERAGE.md).

После правки каталога проверьте данные и соберите пакет:

```powershell
python tools\validate.py
.\Build.cmd
```

`validate.py` печатает JSON-отчёт и завершается с кодом 0, если ошибок нет. Он
проверяет плейсхолдеры, разметку, ключи и привязки, но не исполняет и не имитирует
C#-переводчик: поведение перевода проверяют NUnit-тесты внутри сборки. С
`--evidence <папка>` валидатор дополнительно сверяет каталоги с распакованным исходным
архивом переводов; для этого нужен PyYAML. Без этого параметра сверка помечается
пропущенной.

Контроли самого валидатора — pytest-тесты, которые портят данные и ждут отказа.
Запустить их можно без глобальной установки pytest:

```powershell
uv run --no-project --python 3.11 --with pytest==9.1.1 python -m pytest tests/test_validation.py -q -p no:cacheprovider
```

## Проверки

### Проверка готового пакета

`Build.ps1` отклоняет ZIP, который нарушает правила Thunderstore:

- в корне должны быть `manifest.json`, `icon.png` и `README.md`, иконка — PNG 256×256;
- манифест — в UTF-8 без BOM;
- `name` состоит из `[A-Za-z0-9_]`, `version_number` вида `Major.Minor.Patch` совпадает
  с версией сборки;
- `description` не длиннее 250 символов, `website_url` присутствует;
- зависимости имеют вид `Команда-Пакет-Версия`.

Кроме того, проверяется раскладка для Gale: всё, что загружает плагин, должно лежать
под `plugins/`. Gale кладёт содержимое `plugins/` в `BepInEx/plugins/<пакет>/` с
сохранением подпапок, а файлы вне распознаваемых папок раскладывает плоско. Папка
`catalog/` в корне архива потеряла бы структуру, и плагин не нашёл бы каталоги. Лишние
и недостающие записи, обратные слеши и дубликаты отклоняются. DLL и каталоги внутри ZIP
сверяются по SHA-256 с протестированными файлами, `build-receipt.json` — с упакованной
DLL.

После правки этой проверки запустите её контроли на собранном пакете:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-PackageCheck.ps1
```

Скрипт берёт функции прямо из `Build.ps1` и по умолчанию проверяет самый новый ZIP в
`artifacts/`; другой пакет передаётся параметром `-Package`. Корректный пакет должен
приниматься, а шесть намеренно испорченных копий — отклоняться: каталоги вне
`plugins/`, BOM в манифесте, иконка 128×128, длинное описание, чужая DLL, нет
`README.md`.

### Только тесты

Без упаковки NUnit-тесты запускаются так (пути — как для сборки):

```powershell
dotnet test .\tests\Tolmach.Tests.csproj -c Release `
  '-p:GamePath=C:\Program Files (x86)\Steam\steamapps\common\Valheim' `
  "-p:ProfilePath=$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default" `
  "-p:NewtonsoftJsonPath=$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default\BepInEx\plugins\ValheimModding-JsonDotNET\Newtonsoft.Json.dll"
```

## Выпуск версии

Релизы выпускает [release-kit](https://github.com/Muratovnik/release-kit) по
[relkit.toml](../relkit.toml). Он собирает пакет из закоммиченного кода, проверяет его и
публикует релиз GitHub с ZIP и `SHA256SUMS`. Для выпуска нужен установленный хук перед
push (см. [ниже](#хук-перед-push)).

1. Поднимите `<Version>` в `Tolmach.csproj` и `PluginVersion` в `src/Plugin.cs`. Тест
   сверяет их между собой, release-kit берёт номер версии из csproj.
2. Добавьте в начало `CHANGELOG.md` запись `## <версия> — <заголовок>`. Она входит в
   пакет, видна в менеджере модов и становится текстом релиза.
3. Закоммитьте изменения: release-kit работает только с чистым деревом.
4. Задайте пути к игре и профилю и выпустите релиз:

   ```powershell
   $env:TOLMACH_GAME_PATH = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
   $env:TOLMACH_PROFILE_PATH = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default"
   python .github/relkit.pyz release plan <версия>
   python .github/relkit.pyz release run <версия> --publish --prepare --plan-hash <хеш>
   ```

   `plan` показывает, что будет опубликовано, и печатает `Plan SHA-256` — его и нужно
   передать в `--plan-hash`. Итог успешного `run` — строка
   `published and verified v<версия>`.

`release run … --prepare` выполняет по порядку:

1. **checks** в рабочей копии: `tools/validate.py` и pytest-контроли валидатора.
2. Аудит публикации по рабочему дереву и по всей истории Git: секреты (Betterleaks),
   локальные ссылки в Markdown (Lychee), правила из `[exposure]` и приватные правила
   владельца.
3. **build** в снимке закоммиченного дерева: `Build.ps1 -OutputDirectory …`.
   Незакоммиченные файлы и `artifacts/` рабочей копии в снимок не попадают.
4. **smoke**: `tools/Test-PackageCheck.ps1` на готовом ZIP.
5. Тег `v<версия>`, отправка тега и ветки `main`, черновик релиза с ZIP и `SHA256SUMS`,
   сверка загруженных файлов с подготовленными, публикация и повторная проверка
   скачанного релиза.

Если подготовка упала до создания тега, ничего не опубликовано. Причина и путь к журналу
выводятся в консоль; квитанции и журналы попыток хранятся в `.git/relkit/`. Исправьте
причину, закоммитьте исправление и снова выполните `plan`: вместе с коммитом изменится
и хеш плана. Если выпуск прервался после создания тега, проверьте состояние командой
`release status <версия>` и продолжите через `release resume <версия> --publish`.
`release verify <версия>` перепроверяет уже опубликованный релиз.

Релизы в репозитории неизменяемые (GitHub immutable releases): опубликованный релиз
нельзя перезаписать, исправление выходит новой версией.

На thunderstore.io пакет не публикуется автоматически. Для этого нужен токен команды
Thunderstore `Muratovnik`; ZIP из релиза публикуется командой
`dotnet tool run tcli publish --file <ZIP>`.

## Хук перед push

Выпуск требует хук release-kit в `.git/hooks/pre-push` (`require_guard` и `owner_audit`
в `relkit.toml`). Перед каждым push хук проверяет всю историю, в том числе по приватным
правилам владельца: точным значениям и шаблонам, которым нельзя попадать в публичный
репозиторий. Эти правила не хранятся в репозитории. Хук также закрепляет хеши
`relkit.toml`, `.betterleaks.toml` и `.github/relkit.pyz` и отказывает, если они
изменились без его обновления.

В новом клоне:

1. Укажите папку с приватными правилами владельца:
   `git config --local releasekit.privateRoot <папка>`.
2. Посмотрите план установки и возьмите из него `plan_sha256`:
   `python .github/relkit.pyz protect install --dry-run --json`.
3. Установите хук и проверьте его:

   ```powershell
   python .github/relkit.pyz protect install --plan-hash <plan_sha256>
   python .github/relkit.pyz protect check
   ```

После осознанной правки `relkit.toml`, `.betterleaks.toml` или обновления `relkit.pyz`
обновите закреплённые хеши:

```powershell
python .github/relkit.pyz update --refresh-guard --dry-run
python .github/relkit.pyz update --refresh-guard
python .github/relkit.pyz protect check
```

До коммита то же можно проверить вручную: `python .github/relkit.pyz audit` проверяет
рабочее дерево, `python .github/relkit.pyz audit --staged --owner` — индекс вместе с
приватными правилами.
