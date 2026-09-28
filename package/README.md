# Tolmach

![Книга рун с надписью «Толмач» на перилах у фьорда с драккаром](https://raw.githubusercontent.com/Muratovnik/Tolmach/main/docs/images/banner.jpg)

*Russian translations for 42 Valheim mods and the game's closed captions. Client-side, display text only.*

**Tolmach** (от старинного «толмач» — переводчик) переводит на русский 42 мода Valheim и
субтитры самой игры. Другие пакеты перевода не нужны. Tolmach заполняет только
отсутствующие и английские строки: русский текст самого мода или другого пакета перевода
остаётся как есть.

## Возможности

- Переводит подписи интерфейса, подсказки, сообщения, названия предметов, существ и
  навыков модов из списка ниже, а также субтитры звуков игры.
- Меняет только текст на экране. Сохранения, рецепты, предметы и конфиги модов не
  затрагиваются.
- Работает только на клиенте: серверу и другим игрокам пакет не нужен.
- Позволяет отключить перевод отдельного мода или весь пакет в настройках.

## Установка

Нужны:

- **BepInExPack_Valheim** и **JsonDotNET** — Gale доустанавливает их сам как
  зависимости пакета.

Если пакет ещё не в менеджере модов, скачайте `Muratovnik-Tolmach-<версия>.zip` со
страницы [релизов на GitHub](https://github.com/Muratovnik/Tolmach/releases). Рядом
лежит `SHA256SUMS` для сверки хеша архива.

- **Gale:** «Импорт» → «…локальный мод» и выберите ZIP, или перетащите ZIP в окно
  Gale. Повторный импорт новой версии заменяет прежнюю.
- **r2modman:** Settings → Import local mod. Этот путь не проверялся.
- **Вручную:** скопируйте содержимое папки `plugins` из архива в
  `BepInEx/plugins/Tolmach/` так, чтобы `Tolmach.dll` и папка `catalog` лежали рядом.

Перед первым запуском имеет смысл сделать резервную копию профиля и миров.

## Первый запуск

1. Запустите Valheim через менеджер модов.
2. В настройках игры выберите русский язык.
3. Откройте окно или предмет переводимого мода: текст должен быть на русском.

Если перевода нет, откройте отчёт `BepInEx/config/Tolmach.runtime.txt`. Строка
`Russian active: True` означает, что перевод включён. Ниже — строка по каждому
найденному моду и причина пропуска для модов, которых нет в профиле.

## Переводимые моды

Экранные адаптеры привязаны к версиям ниже. Если у мода другая версия, словарные
переводы (ключи локализации) продолжают работать, а перехват экранного текста этого
мода по умолчанию отключается. Моды, которых нет в профиле, пропускаются.

| Мод | Версия | Мод | Версия |
|---|---|---|---|
| Advize-PlantEasily | 2.2.2 | Marlthon-SeaAnimals | 0.3.9 |
| Advize-PlantEverything | 1.21.3 | MilkMediaProductions-ExpertExplorer | 1.7.0 |
| Azumatt-AzuAreaRepair | 1.1.8 | MSchmoecker-DynamicStoragePiles | 0.8.1 |
| Azumatt-AzuAutoStore | 3.1.6 | MSchmoecker-HammerTime | 0.3.11 |
| Azumatt-AzuCraftyBoxes | 1.8.26 | Nextek-SpeedyPaths | 1.0.9 |
| Azumatt-Build_Camera_Custom_Hammers_Edition | 1.3.3 | OdinPlus-CraftyCartsRemake | 3.2.3 |
| Azumatt-Recycle_N_Reclaim | 1.4.5 | Pumpkin-ValheimVisualEnhanced | 0.5.18 |
| Azumatt-SleepSkip | 1.3.2 | RandyKnapp-EquipmentAndQuickSlots | 3.1.3 |
| Balrond-balrond_humanoidRandomizer | 1.6.0 | SephrinMods-VikingNPC_Continued | 0.4.1 |
| Balrond-balrond_shipyard | 1.7.3 | shudnal-TradersExtended | 2.0.4 |
| blacks7ar-Fermenting | 1.1.8 | sighsorry-UsefulRunestones | 1.0.3 |
| blacks7ar-OreMines | 1.2.1 | Smoothbrain-ComfortTweaks | 3.3.11 |
| blacks7ar-RenegadeVikings | 1.4.2 | Smoothbrain-Groups | 1.2.12 |
| blacks7ar-SeedBed | 1.2.9 | Smoothbrain-Mining | 1.1.7 |
| blacks7ar-SNEAKer | 1.1.8 | Smoothbrain-StaminaRegenerationFromFood | 1.5.8 |
| Cartur-Carturs_Feeding_Trough | 1.0.0 | Therzie-Warfare | 1.9.4 |
| Digitalroot-Digitalroots_GoldBars | 1.2.34 | trustworthy-MaxwellTheCat | 0.0.1 |
| ishid4-BetterArchery | 2.0.2 | ValheimModding-Jotunn | 2.30.2 |
| JereKuusela-Structure_Tweaks | 1.37.0 | Vapok-AdventureBackpacks | 2.2.1 |
| Jumpingmushroom-PortalLines | 0.8.0 | WackyMole-WackysDatabase | 2.5.35 |
| Marlthon-AirAnimals | 0.3.2 | warpalicious-More_World_Locations_AIO | 5.1.4 |

Кроме модов, переводятся субтитры звуков самой игры (Valheim 1.0.16): в её русской
локализации их нет.

## Настройки

Файл `BepInEx/config/muratovnik.tolmach.cfg` появляется после первого запуска.
Изменения применяются после перезапуска игры.

| Параметр | По умолчанию | Назначение |
|---|---|---|
| `General.Enabled` | `true` | Включить весь пакет. |
| `Modules.<мод>` | `true` | Включить перевод отдельного мода. |
| `Display.TranslateStandardMapLabels` | `true` | Переводить на экране стандартные подписи меток карты (например, Camp). Сохранённое имя метки не меняется. Личная метка с таким же именем тоже отобразится переведённой. |
| `Compatibility.AllowOtherVersions` | `false` | Включить экранные адаптеры и для других версий модов. Работа не проверена. |

## Ограничения

- Пакет проверен автоматическими тестами с библиотеками игры и BepInEx. Проверка в
  самой игре на всех экранах ещё не проводилась.
- Не переводятся: имена консольных команд и их аргументы, окно настроек F1,
  пользовательские конфиги и введённые игроком имена.
- Строки, которые мод создаёт при запуске игры (описания улучшений кораблей, названия
  святилищ), появляются на языке, выбранном при запуске; после смены языка их обновит
  перезапуск.
- Ошибки самих модов пакет не исправляет.

О непереведённых местах сообщайте в
[issues на GitHub](https://github.com/Muratovnik/Tolmach/issues). Укажите точную
английскую строку, мод и его версию, экран и строки этого мода из `Tolmach.runtime.txt`.

Tolmach вдохновлён пакетом ObeliskRU. Перевод сделан заново с английских оригиналов
модов.

## Удаление

Закройте игру и удалите мод в менеджере (или папку `BepInEx/plugins/Tolmach`), затем
запустите игру снова.
