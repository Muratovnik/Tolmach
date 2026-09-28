# Основания реализации

## Входные данные

Единственный источник состава и конкретных игровых строк — предоставленный
архив `valheim-handoff-2026-09-27-2300.zip`, а не произвольные последние версии
модов из сети. Хеш архива и список источников находятся в `PROVENANCE.json`.

Использованы `localization/coverage.json`, `localization/UNTRANSLATED.md`,
`inventory/current.json`, `inventory/binary-identities.json`, английские ресурсы
из `packages/`, активные текстовые конфиги и декомпиляции из `source-evidence/`.
Поле `sourceFiles` каждого каталога указывает основные относительные пути внутри
входного архива. Оно не означает, что каждая строка встретилась именно в
главном Plugin.cs: для игровых сообщений также изучались вспомогательные классы.

Перевод сделан с английских оригиналов модов. Модифицированные сборки исходных модов
не распространяются.

Источники 0.3.0: декомпиляции Jotunn 2.30.2, AdventureBackpacks 2.2.1 (из профиля),
AzuAreaRepair 1.1.8, PlantEasily 2.2.2 и Humanoid Randomizer 1.6.0; встроенные asset
bundle Shipyard и Humanoid Randomizer, прочитанные UnityPy; таблица
`localization_captions` и `Version.cs` игры 1.0.16. Термины сверены с русской
локализацией игры из `resources.assets`.

Источники 0.4.0: последние версии 28 модов на Thunderstore (28 сентября 2026), декомпилированные
ilspycmd; английские ресурсы и код этих версий; IL-дампы для строк, которые декомпилятор показывает
интерполяцией. У модов со своим русским переводом его файлы использовались только для списка уже
переведённых ключей и для единства названий предметов этих модов.

## Первичная документация

- Jötunn: Localization — https://valheim-modding.github.io/Jotunn/tutorials/localization.html
  Собственные CustomLocalization по GUID, ключи категорий, событие OnLocalizationAdded.
- BepInEx: Creating a new plugin project — https://docs.bepinex.dev/v5.4.16/articles/dev_guide/plugin_tutorial/2_plugin_start.html
  Метаданные, мягкие зависимости, ссылки на библиотеки. Учтено предупреждение
  не подмешивать системные DLL игры к стандартной библиотеке компилятора.
- Harmony: Transpiler — https://harmony.pardeike.net/v2/articles/patching-transpiler.html
  Ограниченные изменения IL и совместимость с другими транспайлерами.

Документация использована для проектирования, но не заменяет проверку реального
исполнения на версиях библиотеки и игры из пользовательского профиля.

## Исправления 0.1.1 и метод ревью

Assay использован на зафиксированной ревизии, без выполнения evals и без изменения самого репозитория:

- https://github.com/Muratovnik/assay/blob/171a951c71eced1a9a99dd769bc78ae85db421eb/skills/code-change/SKILL.md
- https://github.com/Muratovnik/assay/blob/171a951c71eced1a9a99dd769bc78ae85db421eb/skills/test-writing/SKILL.md
- https://github.com/Muratovnik/assay/blob/171a951c71eced1a9a99dd769bc78ae85db421eb/skills/independent-audit/SKILL.md
- Harmony PatchProcessor / GetOriginalInstructions: https://harmony.pardeike.net/v2/api/HarmonyLib.PatchProcessor.html
- Jötunn: https://valheim-modding.github.io/Jotunn/tutorials/localization.html
- BepInEx 5: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/1_setup.html

Точные привязки модов основаны на пользовательском handoff, не на текущих релизах
в интернете. Снимок метаданных: tests/fixtures/snapshot-bindings.json. Идентичность
проверяемого источника позволяет повторить сверку, но не подтверждает загрузку DLL.


## Переиспользование 0.1.2

Источники просмотрены 28 сентября 2026. Не подменяют проверку конкретного бинарника.

- Assay reuse: https://github.com/Muratovnik/assay/blob/171a951c71eced1a9a99dd769bc78ae85db421eb/skills/code-change/references/reuse-and-migration.md
- Assay reuse review: https://github.com/Muratovnik/assay/blob/171a951c71eced1a9a99dd769bc78ae85db421eb/skills/independent-audit/references/solution-choices-and-reuse.md
- Harmony AccessTools: https://harmony.pardeike.net/v2/api/HarmonyLib.AccessTools.html
- Harmony label extensions: https://harmony.pardeike.net/v2/api/HarmonyLib.CodeInstructionExtensions.html
- MSBuild GetFileHash: https://learn.microsoft.com/en-us/visualstudio/msbuild/getfilehash-task
- MSBuild assembly attributes: https://learn.microsoft.com/en-us/visualstudio/msbuild/writecodefragment-task
- NUnit: https://docs.nunit.org/articles/nunit/getting-started/installation.html
- Fixed packages: https://www.nuget.org/packages/NUnit/3.14.0 ; https://www.nuget.org/packages/NUnit3TestAdapter/4.6.0 ; https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.11.1 ; https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies/1.0.3
- XUnity.AutoTranslator manual translations/API/Mono hooks: https://github.com/bbepis/XUnity.AutoTranslator

Фактические сигнатуры Jötunn (`in string`) дополнительно проверены по приложенным
`Jotunn.Entities/CustomLocalization.cs` и `Jotunn.Managers/LocalizationManager.cs`
в `source-evidence/localization/a/ValheimModding-Jotunn/`.

## Пакет и установка 0.1.3

Источники просмотрены 28 сентября 2026.

- Thunderstore, формат пакета (иконка 256×256, README.md, manifest.json, ограничения полей):
  https://wiki.thunderstore.io/mods/creating-a-package
- Thunderstore CLI 0.2.4 (сборка по `thunderstore.toml`, `--package-version`, приоритет
  параметров CLI): https://github.com/thunderstore-io/thunderstore-cli/tree/0.2.4 ,
  https://www.nuget.org/packages/tcli/0.2.4
- Категории сообщества Valheim: https://thunderstore.io/api/experimental/community/valheim/category/
- Gale 1.22.3, импорт локального мода и установщик BepInEx-пакетов (раскладка `plugins/`,
  плоское размещение прочих файлов, имя папки по `manifest.name`):
  https://github.com/Kesomannen/gale/blob/1.22.3/src-tauri/src/profile/import/local.rs ,
  https://github.com/Kesomannen/gale/blob/1.22.3/src-tauri/src/profile/install/installers/subdir.rs ,
  https://github.com/Kesomannen/gale/blob/1.22.3/src-tauri/src/game/mod_loader.rs
- Названия пунктов меню Gale: https://github.com/Kesomannen/gale/blob/1.22.3/messages/ru-RU.json

Сигнатуры игры (`Localization` в `assembly_guiutils.dll`, `Terminal.AddString`,
`Chat.OnNewChatMessage`, `Minimap.CreateMapNamePin`, `PlatformPrefs`) проверены
декомпиляцией установленной Valheim 1.0.16. Jötunn 2.30.2, LocalizationCache 0.3.0
и встроенные LocalizeKey модов — по их DLL из профиля. Декомпиляции
в поставку не включены.
