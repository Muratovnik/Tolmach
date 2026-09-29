# Tolmach

![Книга рун с надписью «Толмач» на перилах у фьорда с драккаром](https://raw.githubusercontent.com/Muratovnik/Tolmach/main/docs/images/banner.jpg)

*Russian translations for 87 Valheim mods and the game's closed captions. Client-side, display text only.*

**Tolmach** (от старинного «толмач» — переводчик) переводит на русский 87 модов Valheim и
субтитры самой игры. Другие пакеты перевода не нужны. Tolmach заполняет только
отсутствующие и английские строки: русский текст самого мода или другого пакета перевода
остаётся как есть, кроме нескольких явно отмеченных исправлений ошибок в нем.

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

Пакет опубликован на [Thunderstore](https://thunderstore.io/c/valheim/p/Muratovnik/Tolmach/):
в Gale или r2modman найдите Tolmach в списке модов и установите, как любой мод Thunderstore.

Установить вручную можно ZIP `Muratovnik-Tolmach-<версия>.zip` со страницы
[релизов на GitHub](https://github.com/Muratovnik/Tolmach/releases). Рядом лежит
`SHA256SUMS` для сверки хеша архива.

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

Переводы сверены с версиями ниже. Если у мода другая версия, Tolmach переводит строки,
которые в ней находит, а новые строки мода остаются английскими; предупреждение о версии
и ненайденные строки видны в `Tolmach.runtime.txt`. Моды, которых нет в профиле,
пропускаются.

| Мод | Версия | Мод | Версия |
|---|---|---|---|
| Advize-PlantEasily | 2.2.2 | Korppis-SolidHitboxes | 1.0.8 |
| Advize-PlantEverything | 1.21.3 | Korppis-Spearfishing | 1.0.5 |
| Alpus-Transmog | 3.2.0 | LJS-BossAdd | 1.2.0 |
| Azumatt-AzuAreaRepair | 1.1.8 | lnsanity-Culinary_Horizons | 1.0.42 |
| Azumatt-AzuAutoStore | 3.1.6 | M2Valheim-SocialSystem | 1.0.1 |
| Azumatt-AzuCraftyBoxes | 1.8.26 | Marlthon-AirAnimals | 0.3.2 |
| Azumatt-AzuWorkbenchTweaks | 1.0.7 | Marlthon-SeaAnimals | 0.3.9 |
| Azumatt-Build_Camera_Custom_Hammers_Edition | 1.3.3 | MaxFoxGaming-Better_Beehives | 1.3.0 |
| Azumatt-FirstPersonMode | 1.3.12 | MidnightMods-NetworkPerformanceSystem | 1.11.0 |
| Azumatt-PerfectPlacement | 1.2.2 | MidnightMods-ValheimInfiniteFire | 1.4.0 |
| Azumatt-Recipe_Description_Expansion | 1.1.9 | MilkMediaProductions-ExpertExplorer | 1.7.0 |
| Azumatt-Recycle_N_Reclaim | 1.4.5 | MSchmoecker-DynamicStoragePiles | 0.8.1 |
| Azumatt-SleepSkip | 1.3.2 | MSchmoecker-HammerTime | 0.3.11 |
| Balrond-balrond_amazing_nature | 1.4.0 | MSchmoecker-VNEI | 0.17.6 |
| Balrond-balrond_arsenal_reborn | 0.1.6 | Nextek-SpeedyPaths | 1.0.9 |
| Balrond-balrond_constructions | 1.4.5 | NickPappas-Location_Placement_Accelerator | 1.0.24 |
| Balrond-balrond_DualMastery | 0.2.7 | NightOfGames-Huginn_Map | 1.0.5 |
| Balrond-balrond_furniture_reborn | 1.2.9 | OdinPlus-CraftyCartsRemake | 3.2.4 |
| Balrond-balrond_humanoidRandomizer | 1.6.0 | OdinPlus-OdinsSteelworks | 0.4.1 |
| Balrond-balrond_lightkeeper | 1.0.2 | OdinPlus-QuickTeleport | 2.0.4 |
| Balrond-balrond_shipyard | 1.7.3 | OverDrive-SmelterUpgrades | 1.1.5 |
| BentoG-MissingPieces | 2.3.2 | Pumpkin-ValheimVisualEnhanced | 0.5.18 |
| blacks7ar-Fermenting | 1.1.8 | RandyKnapp-EpicLoot | 0.14.13 |
| blacks7ar-FoodDurationMultiplier | 1.1.7 | RandyKnapp-EquipmentAndQuickSlots | 3.1.3 |
| blacks7ar-OreMines | 1.2.1 | SephrinMods-VikingNPC_Continued | 0.4.1 |
| blacks7ar-RenegadeVikings | 1.4.2 | shudnal-MyLittleUI | 1.2.26 |
| blacks7ar-SeedBed | 1.2.9 | shudnal-TradersExtended | 2.0.4 |
| blacks7ar-SNEAKer | 1.1.8 | sighsorry-InventorySlots | 1.5.15 |
| blacks7ar-TorchesAreFires | 1.1.0 | sighsorry-UsefulRunestones | 1.0.3 |
| blacks7ar-WieldEquipmentWhileSwimming | 1.1.4 | Smoothbrain-ComfortTweaks | 3.3.11 |
| Cartur-Carturs_Feeding_Trough | 1.0.0 | Smoothbrain-CreatureLevelAndLootControl | 4.6.4 |
| Digitalroot-Digitalroots_GoldBars | 1.2.34 | Smoothbrain-Groups | 1.2.12 |
| DragonMotion-MoveBuildPieces | 1.1.1 | Smoothbrain-Mining | 1.1.7 |
| Dreanegade-Eternal_Legends | 1.0.4 | Smoothbrain-PassivePowers | 1.1.5 |
| Dreanegade-Hunter_Legacy | 1.1.4 | Smoothbrain-StaminaRegenerationFromFood | 1.5.8 |
| Dreanegade-Magic_Supremacy | 3.1.2 | SpikeHimself-XPortal | 1.2.25 |
| Goldenrevolver-Quick_Stack_Store_Sort_Trash_Restock | 1.4.15 | Therzie-Armory | 1.4.2 |
| hoskope-TakeAllCooked | 1.1.0 | Therzie-Warfare | 1.9.4 |
| ishid4-BetterArchery | 2.0.2 | trustworthy-MaxwellTheCat | 0.0.1 |
| JereKuusela-Infinity_Hammer | 1.85.0 | ValheimModding-Jotunn | 2.30.2 |
| JereKuusela-Server_devcommands | 1.115.0 | Vapok-AdventureBackpacks | 2.2.2 |
| JereKuusela-Structure_Tweaks | 1.37.0 | WackyMole-WackysDatabase | 2.5.36 |
| JereKuusela-World_Edit_Commands | 1.78.0 | warpalicious-More_World_Locations_AIO | 5.1.5 |
| Jumpingmushroom-PortalLines | 0.8.0 | | |

Кроме модов, переводятся субтитры звуков самой игры (Valheim 1.0.16): в её русской
локализации их нет.

У модов со своим русским переводом (Amazing Nature, Epic Loot, Culinary Horizons, Transmog,
Quick Stack Store и другие) Tolmach дописывает только недостающие строки. Valheim Armory от
MidnightMods и ImpactfulSkills переведены самими модами полностью; Armory от Therzie — другой
мод, его переводит Tolmach. В полном русском MissingPieces Tolmach исправляет три строки с
ошибками. У Recipe Description Expansion части комплекта в подсказке показываются названиями
предметов из игры, а не внутренними именами.

## Настройки

Файл `BepInEx/config/muratovnik.tolmach.cfg` появляется после первого запуска.
Изменения применяются после перезапуска игры.

| Параметр | По умолчанию | Назначение |
|---|---|---|
| `General.Enabled` | `true` | Включить весь пакет. |
| `Modules.<мод>` | `true` | Включить перевод отдельного мода. |
| `Display.TranslateStandardMapLabels` | `true` | Переводить на экране стандартные подписи меток карты (например, Camp). Сохранённое имя метки не меняется. Личная метка с таким же именем тоже отобразится переведённой. |
| `Compatibility.OnlyAuditedVersions` | `false` | Для модов других версий оставить только словарные переводы, без перевода строк в коде мода. |

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
