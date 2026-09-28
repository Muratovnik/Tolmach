# Tolmach

*Russian translations for 37 Valheim mods that ObeliskRU leaves untranslated or partly translated. Client-side, display text only.*

**Толмач** (старинное русское «переводчик») дополняет **ObeliskRU**: переводит на
русский 37 модов, которые не переведены или переведены частично. Пакет меняет только
текст на экране. Сохранения, рецепты, предметы и конфиги модов он не трогает. Уже
существующий русский перевод, в том числе от ObeliskRU, не заменяется: пакет заполняет
только отсутствующие и английские строки.

## Что нужно

- **BepInExPack_Valheim** и **JsonDotNET** — Gale и r2modman ставят их автоматически
  как зависимости.
- **ObeliskRU** — рекомендуется оставить включённым: этот пакет его дополняет.
- В настройках Valheim выбран русский язык.

Перевод работает только на клиенте. Другим игрокам и серверу пакет не нужен.

## Установка

Скачайте `Muratovnik-Tolmach-<версия>.zip` со страницы
[релизов на GitHub](https://github.com/Muratovnik/Tolmach/releases). Рядом лежит
`SHA256SUMS` — по нему можно сверить хеш скачанного архива.

- **Gale:** меню «Импорт» → «…локальный мод» и выберите ZIP. Можно просто перетащить
  ZIP в окно Gale.
- **r2modman:** Settings → Import local mod.
- **Вручную:** скопируйте содержимое папки `plugins` из архива в
  `BepInEx/plugins/Tolmach/` так, чтобы `Tolmach.dll` и папка `catalog` лежали рядом.

Перед первым запуском имеет смысл сделать резервную копию профиля и миров.

## Какие моды переводятся

Экранные адаптеры привязаны к версиям ниже. Если у мода другая версия, словарные
переводы (ключи локализации) продолжают работать, а перехват экранного текста этого
мода по умолчанию отключается.

| Мод | Версия | Мод | Версия |
|---|---|---|---|
| Advize-PlantEverything | 1.21.3 | Marlthon-SeaAnimals | 0.3.9 |
| Azumatt-AzuAutoStore | 3.1.6 | MilkMediaProductions-ExpertExplorer | 1.7.0 |
| Azumatt-AzuCraftyBoxes | 1.8.26 | MSchmoecker-DynamicStoragePiles | 0.8.1 |
| Azumatt-Build_Camera_Custom_Hammers_Edition | 1.3.3 | MSchmoecker-HammerTime | 0.3.11 |
| Azumatt-Recycle_N_Reclaim | 1.4.5 | Nextek-SpeedyPaths | 1.0.9 |
| Azumatt-SleepSkip | 1.3.2 | OdinPlus-CraftyCartsRemake | 3.2.3 |
| Balrond-balrond_shipyard | 1.7.3 | Pumpkin-ValheimVisualEnhanced | 0.5.18 |
| blacks7ar-Fermenting | 1.1.8 | RandyKnapp-EquipmentAndQuickSlots | 3.1.3 |
| blacks7ar-OreMines | 1.2.1 | SephrinMods-VikingNPC_Continued | 0.4.1 |
| blacks7ar-RenegadeVikings | 1.4.2 | shudnal-TradersExtended | 2.0.4 |
| blacks7ar-SeedBed | 1.2.9 | sighsorry-UsefulRunestones | 1.0.3 |
| blacks7ar-SNEAKer | 1.1.8 | Smoothbrain-ComfortTweaks | 3.3.11 |
| Cartur-Carturs_Feeding_Trough | 1.0.0 | Smoothbrain-Groups | 1.2.12 |
| Digitalroot-Digitalroots_GoldBars | 1.2.34 | Smoothbrain-Mining | 1.1.7 |
| ishid4-BetterArchery | 2.0.2 | Smoothbrain-StaminaRegenerationFromFood | 1.5.8 |
| JereKuusela-Structure_Tweaks | 1.37.0 | Therzie-Warfare | 1.9.4 |
| Jumpingmushroom-PortalLines | 0.8.0 | trustworthy-MaxwellTheCat | 0.0.1 |
| Marlthon-AirAnimals | 0.3.2 | WackyMole-WackysDatabase | 2.5.35 |
| | | warpalicious-More_World_Locations_AIO | 5.1.4 |

Не установленные у вас моды просто пропускаются.

## Настройки

Файл `BepInEx/config/muratovnik.tolmach.cfg`, изменения применяются после
перезапуска игры:

| Параметр | По умолчанию | Назначение |
|---|---|---|
| `General.Enabled` | `true` | Включить весь пакет. |
| `Modules.<мод>` | `true` | Включить перевод отдельного мода. |
| `Display.TranslateStandardMapLabels` | `true` | Переводить на экране стандартные подписи меток карты (например, Camp). Сохранённое имя метки не меняется. Личная метка с таким же именем тоже отобразится переведённой. |
| `Compatibility.AllowOtherVersions` | `false` | Включить экранные адаптеры и для других версий модов. Работа не проверена. |

После запуска пакет пишет отчёт `BepInEx/config/Tolmach.runtime.txt`: какие
моды найдены, какие адаптеры установлены и какие предупреждения возникли.

## Ограничения

- Пакет собран и проверен автоматическими тестами с библиотеками игры и BepInEx.
  Проверка в самой игре на всех экранах ещё не проводилась. Сообщайте о непереведённых
  местах в [issues на GitHub](https://github.com/Muratovnik/Tolmach/issues): точную
  английскую строку, мод и версию, экран и строки из `runtime.txt`.
- Не переводятся: команды и вывод консоли, окно настроек F1, пользовательские конфиги
  и введённые игроком имена, строки из других версий модов.
- Ошибки самих модов пакет не исправляет.

## Удаление

Закройте игру и удалите мод в менеджере (или папку `BepInEx/plugins/Tolmach`),
затем запустите игру снова.
