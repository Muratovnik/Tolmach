# Tolmach

Исходники BepInEx-плагина для Valheim, который добавляет русский перевод 37 модам:
24 непереведённым и 13 частично переведённым. Название — от старинного русского
«толмач», переводчик. Пакет дополняет **ObeliskRU**: заполняет только отсутствующие и
английские строки и меняет лишь текст на экране. Результат сборки — пакет Thunderstore,
который Gale и r2modman импортируют как локальный мод. Готовые пакеты публикуются в
[релизах GitHub](https://github.com/Muratovnik/Tolmach/releases). Описание для
игроков, которое показывается в менеджере модов, — [package/README.md](package/README.md).

## Сборка пакета

Нужны Windows, .NET SDK 8 или новее (проверено с 9.0.318), .NET Framework 4.8 и
установленная Valheim. Нужен и профиль менеджера модов с BepInEx 5 и JsonDotNET:
из него берутся библиотеки для компиляции и тестов. При первой сборке NuGet скачивает
зафиксированные пакеты разработки и Thunderstore CLI; источник задан в
[nuget.config](nuget.config).

Сборка ничего не пишет ни в игру, ни в профиль, поэтому игру закрывать не нужно.

1. Запустите `Build.cmd` и укажите папку Valheim и папку профиля. Можно передать их
   сразу (пути этого компьютера):

   ```powershell
   .\Build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Valheim' `
     -ProfilePath "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default"
   ```

   Вместо параметров можно один раз задать переменные окружения `TOLMACH_GAME_PATH` и
   `TOLMACH_PROFILE_PATH`: их читают и `Build.ps1`, и выпуск через release-kit.

2. Готовый пакет: `artifacts/Muratovnik-Tolmach-<версия>.zip`. Журналы, TRX и
   `build-receipt.json` этого запуска лежат в `artifacts/run-<GUID>/`.

Пакет появляется, только если все тесты выполнены и прошли, DLL и 37 каталогов
совпадают с протестированными, а готовый ZIP прошёл проверку формата Thunderstore и
раскладки Gale. Подробности и ручные команды: [docs/BUILD.md](docs/BUILD.md).

## Установка собранного пакета

- Gale: «Импорт» → «…локальный мод», выбрать ZIP (или перетащить его в окно).
- Из проекта `valheim` командой, которая создаёт резервную копию профиля:
  `node gale.mjs install-local '<путь к ZIP>'`.
- r2modman: Settings → Import local mod.

Менеджер установит зависимости из манифеста (BepInExPack_Valheim, JsonDotNET) и
разложит файлы в `BepInEx/plugins/Tolmach/`. Повторный импорт заменяет прежнюю
версию локального мода.

## Изменение переводов

Каждый мод описан файлом в [catalog/](catalog/): идентичность (GUID, имя сборки,
версия плагина, namespaces), словарные ключи `words`/`englishWords`, точные строки
`texts`, шаблоны `patterns`, адресные литералы `literals` и другие адаптеры. Формат
и политика переводов — [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), покрытие по
модам — [docs/COVERAGE.md](docs/COVERAGE.md).

После правки каталога запустите проверку данных и сборку:

```powershell
python tools\validate.py
.\Build.cmd
```

`validate.py` проверяет плейсхолдеры, разметку, ключи и привязки без выполнения C#.
Поведение перевода проверяют NUnit-тесты, которые запускает сборка.

## Выпуск новой версии

Релизы выпускает [release-kit](https://github.com/Muratovnik/release-kit), закреплённый
в репозитории как `.github/relkit.pyz`; настройки — в [relkit.toml](relkit.toml). Он
собирает пакет из закоммиченного кода, проверяет его и публикует релиз GitHub с ZIP и
`SHA256SUMS`. Нужны Python 3.11+, GitHub CLI 2.98+ с доступом на запись в репозиторий
и те же библиотеки игры и профиля, что для обычной сборки.

1. Поднимите `<Version>` в [Tolmach.csproj](Tolmach.csproj) и `PluginVersion` в
   [src/Plugin.cs](src/Plugin.cs). Тест сверяет их между собой, release-kit берёт номер
   версии из csproj.
2. Добавьте в [CHANGELOG.md](CHANGELOG.md) запись `## <версия> — <заголовок>`. Она
   входит в пакет, видна в менеджере модов и становится текстом релиза.
3. Закоммитьте изменения: release-kit работает только с чистым деревом.
4. Задайте пути к игре и профилю и выпустите релиз:

   ```powershell
   $env:TOLMACH_GAME_PATH = 'D:\SteamLibrary\steamapps\common\Valheim'
   $env:TOLMACH_PROFILE_PATH = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\Default"
   python .github/relkit.pyz release plan <версия>
   python .github/relkit.pyz release run <версия> --publish --prepare --plan-hash <хеш из plan>
   ```

   `plan` показывает, что будет опубликовано, и печатает хеш плана. `run` повторяет
   проверки, аудит секретов и ссылок, сборку и проверку ZIP, затем создаёт тег
   `v<версия>`, отправляет его вместе с веткой `main` и публикует релиз.
5. После сбоя начните с `python .github/relkit.pyz release status <версия>`, затем
   продолжите командой `release resume <версия> --publish`. Опубликованный релиз
   перепроверяет `release verify <версия>`.

Перед коммитом полезно запустить `python .github/relkit.pyz audit`: он ищет секреты,
битые локальные ссылки и нарушения правил из `relkit.toml`.

Выпуск требует хук release-kit перед push (`require_guard`, `owner_audit`). Хук проверяет
всю историю, в том числе по приватным правилам владельца. Они не хранятся в
репозитории. В новом клоне путь к ним задаётся локальной настройкой
`git config --local releasekit.privateRoot <папка>`. Затем хук ставится командами
`python .github/relkit.pyz protect install --dry-run --json` (показывает `plan_sha256`) и
`python .github/relkit.pyz protect install --plan-hash <plan_sha256>`. После
осознанной правки `relkit.toml`, `.betterleaks.toml` или обновления `relkit.pyz` хук
обновляется командой `python .github/relkit.pyz update --refresh-guard`.

На thunderstore.io пакет не публиковался. Для этого нужен токен команды
Thunderstore `Muratovnik`; ZIP из релиза публикуется командой
`dotnet tool run tcli publish --file <ZIP>`.

## Что проверено

Сборка и 167 NUnit-тестов прошли с библиотеками игры, BepInEx 5.4.23 и Harmony из
профиля. Все 37 привязок сверены с установленными DLL модов. Запуск в игре на всех
экранах пока не выполнялся; план приёмки — [docs/RUNTIME-CHECKLIST.md](docs/RUNTIME-CHECKLIST.md),
результаты проверок — [docs/VALIDATION.md](docs/VALIDATION.md).

## Документация

- [docs/BUILD.md](docs/BUILD.md) — сборка, проверка пакета, выпуск, ручные команды.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — устройство плагина и границы интеграции.
- [docs/COVERAGE.md](docs/COVERAGE.md) — что переведено для каждого мода.
- [docs/VALIDATION.md](docs/VALIDATION.md) — выполненные проверки и их границы.
- [docs/RUNTIME-CHECKLIST.md](docs/RUNTIME-CHECKLIST.md) — приёмка в игре.
- [docs/REUSE-DECISIONS.md](docs/REUSE-DECISIONS.md) — готовые решения вместо своих механизмов.
- [docs/SOURCES.md](docs/SOURCES.md) — первичные источники.
- [CHANGELOG.md](CHANGELOG.md), [LICENSE](LICENSE) (MIT; оригинальные строки модов
  принадлежат их авторам).
