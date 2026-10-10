# Tolmach

![Книга рун «Толмач» у фьорда с драккаром](https://raw.githubusercontent.com/Muratovnik/tolmach/main/docs/images/banner.jpg)

Русский перевод для модов Valheim и звуковых субтитров игры: интерфейс, предметы, существа, навыки, подсказки и сообщения.

Работает самостоятельно, без обязательной установки другого русификатора. Дополняет существующий русский перевод; отдельные модули можно отключить в настройках.

[Установка](#установка) · [Моды](#переводимые-моды) · [Настройки](#настройки) · [Помощь](#помощь)

## Установка

Установите Tolmach в нужный профиль Valheim через Gale или r2modman. Менеджер установит BepInExPack_Valheim и JsonDotNET. Tolmach нужен у игрока; серверу он не требуется. Переводимые моды устанавливаются отдельно, отсутствующие моды пропускаются.

Перед обновлением закройте игру. Затем запустите её через выбранный профиль, выберите русский язык и перезапустите после смены языка. Откройте поддерживаемое окно или подсказку из [покрытия](https://github.com/Muratovnik/tolmach/blob/main/docs/COVERAGE.md): перевод применяется автоматически.

Для ZIP из GitHub и ручного копирования файлов есть [отдельная инструкция](https://github.com/Muratovnik/tolmach/blob/main/docs/INSTALLATION.md#установка-из-архива).

## Переводимые моды

В одних модах переведены основные окна и предметы, в других — дополнены отдельные строки или только сообщения о несовпадении версий. Число 103 не означает одинаковую полноту перевода каждого мода.

Ниже указаны версии, с которыми сверялись каталоги. Другие версии обрабатываются по совпадающим строкам, но их совместимость не считается проверенной. Моды, которых нет в профиле, пропускаются.

[Детализация переводов](https://github.com/Muratovnik/tolmach/blob/main/docs/COVERAGE.md)

| Мод                                                                                                                              | Версия  |
| -------------------------------------------------------------------------------------------------------------------------------- | ------- |
| [AAA Crafting](https://thunderstore.io/c/valheim/p/Azumatt/AAA_Crafting/)                                                        | 2.1.11 |
| [Achievement Enabler](https://thunderstore.io/c/valheim/p/MidnightMods/AchievementEnabler/) | 0.4.1 |
| [Adventure Backpacks](https://thunderstore.io/c/valheim/p/Vapok/AdventureBackpacks/)                                             | 2.2.10 |
| [Air Animals](https://thunderstore.io/c/valheim/p/Marlthon/AirAnimals/)                                                          | 0.3.2 |
| [Armory (Therzie)](https://thunderstore.io/c/valheim/p/Therzie/Armory/)                                                          | 1.4.2 |
| [Async Save](https://thunderstore.io/c/valheim/p/MidnightMods/AsyncSave/)                                                        | 0.6.0 |
| [Azu Area Repair](https://thunderstore.io/c/valheim/p/Azumatt/AzuAreaRepair/)                                                    | 1.1.8 |
| [Azu Auto Store](https://thunderstore.io/c/valheim/p/Azumatt/AzuAutoStore/)                                                      | 3.1.7 |
| [Azu Crafty Boxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/)                                                  | 1.8.27 |
| [Azu Workbench Tweaks](https://thunderstore.io/c/valheim/p/Azumatt/AzuWorkbenchTweaks/)                                          | 1.0.7 |
| [Balrond Amazing Nature](https://thunderstore.io/c/valheim/p/Balrond/balrond_amazing_nature/)                                    | 1.4.0 |
| [Balrond Arsenal Reborn](https://thunderstore.io/c/valheim/p/Balrond/balrond_arsenal_reborn/)                                    | 0.1.6 |
| [Balrond Constructions](https://thunderstore.io/c/valheim/p/Balrond/balrond_constructions/)                                      | 1.4.5 |
| [Balrond Dual Mastery](https://thunderstore.io/c/valheim/p/Balrond/balrond_DualMastery/)                                         | 0.2.7 |
| [Balrond Furniture Reborn](https://thunderstore.io/c/valheim/p/Balrond/balrond_furniture_reborn/)                                | 1.2.9 |
| [Balrond Humanoid Randomizer](https://thunderstore.io/c/valheim/p/Balrond/balrond_humanoidRandomizer/)                           | 1.6.1 |
| [Balrond Lightkeeper](https://thunderstore.io/c/valheim/p/Balrond/balrond_lightkeeper/)                                          | 1.0.2 |
| [Balrond Shipyard](https://thunderstore.io/c/valheim/p/Balrond/balrond_shipyard/)                                                | 1.8.4 |
| [Better Archery](https://thunderstore.io/c/valheim/p/ishid4/BetterArchery/)                                                      | 2.0.2 |
| [Better Beehives](https://thunderstore.io/c/valheim/p/MaxFoxGaming/Better_Beehives/)                                             | 1.3.1 |
| [BossAdd](https://thunderstore.io/c/valheim/p/LJS/BossAdd/)                                                                      | 1.2.0 |
| [Build Camera — Custom Hammers Edition](https://thunderstore.io/c/valheim/p/Azumatt/Build_Camera_Custom_Hammers_Edition/)        | 1.3.4 |
| [Cartur's Feeding Trough](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Feeding_Trough/)                                    | 1.0.0 |
| [Circlet Extended](https://thunderstore.io/c/valheim/p/shudnal/CircletExtended/) | 1.1.13 |
| [Comfort Tweaks](https://thunderstore.io/c/valheim/p/Smoothbrain/ComfortTweaks/)                                                 | 3.3.11 |
| [Conditional Config Sync](https://thunderstore.io/c/valheim/p/shudnal/ConditionalConfigSync/)                                    | 1.0.10 |
| [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)                                       | 1.1.25 |
| [Crafty Carts Remake](https://thunderstore.io/c/valheim/p/OdinPlus/CraftyCartsRemake/)                                           | 3.2.4 |
| [Creature Level and Loot Control](https://thunderstore.io/c/valheim/p/Smoothbrain/CreatureLevelAndLootControl/)                  | 4.6.4 |
| [Culinary Horizons](https://thunderstore.io/c/valheim/p/lnsanity/Culinary_Horizons/)                                             | 1.0.42 |
| [Digitalroot's Gold Bars](https://thunderstore.io/c/valheim/p/Digitalroot/Digitalroots_GoldBars/)                                | 1.2.34 |
| [Dynamic Storage Ammunition Piles](https://thunderstore.io/c/valheim/p/Muji/DynamicStorage_Ammunition_Piles/)                    | 1.0.3 |
| [Dynamic Storage Butcher’s Hall](https://thunderstore.io/c/valheim/p/Muji/DynamicStorage_Butchers_Hall/)                         | 1.1.0 |
| [Dynamic Storage Forge](https://thunderstore.io/c/valheim/p/Muji/DynamicStorageForge/)                                           | 1.0.2 |
| [Dynamic Storage Materials](https://thunderstore.io/c/valheim/p/Muji/DynamicStorageMaterials/)                                   | 1.0.2 |
| [Dynamic Storage Piles](https://thunderstore.io/c/valheim/p/MSchmoecker/DynamicStoragePiles/)                                    | 0.8.1 |
| [Epic Loot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/)                                                            | 0.14.13 |
| [Equipment and Quick Slots](https://thunderstore.io/c/valheim/p/RandyKnapp/EquipmentAndQuickSlots/)                              | 3.1.3 |
| [Eternal Legends](https://thunderstore.io/c/valheim/p/Dreanegade/Eternal_Legends/)                                               | 1.0.4 |
| [Expert Explorer](https://thunderstore.io/c/valheim/p/MilkMediaProductions/ExpertExplorer/)                                      | 1.7.0 |
| [Fermenting](https://thunderstore.io/c/valheim/p/blacks7ar/Fermenting/)                                                          | 1.1.8 |
| [First Person Mode](https://thunderstore.io/c/valheim/p/Azumatt/FirstPersonMode/)                                                | 1.3.12 |
| [Food Duration Multiplier](https://thunderstore.io/c/valheim/p/blacks7ar/FoodDurationMultiplier/)                                | 1.1.7 |
| [Groups](https://thunderstore.io/c/valheim/p/Smoothbrain/Groups/)                                                                | 1.2.12 |
| [HammerTime](https://thunderstore.io/c/valheim/p/MSchmoecker/HammerTime/)                                                        | 0.3.11 |
| [Huginn Map](https://thunderstore.io/c/valheim/p/NightOfGames/Huginn_Map/)                                                       | 1.0.5 |
| [Hunter Legacy](https://thunderstore.io/c/valheim/p/Dreanegade/Hunter_Legacy/)                                                   | 1.1.4 |
| [Infinity Hammer](https://thunderstore.io/c/valheim/p/JereKuusela/Infinity_Hammer/)                                              | 1.87.0 |
| [Inventory Slots](https://thunderstore.io/c/valheim/p/sighsorry/InventorySlots/)                                                 | 1.5.15 |
| [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)                                                             | 2.30.2 |
| [Location Placement Accelerator](https://thunderstore.io/c/valheim/p/NickPappas/Location_Placement_Accelerator/)                 | 1.0.24 |
| [Magic Supremacy](https://thunderstore.io/c/valheim/p/Dreanegade/Magic_Supremacy/)                                               | 3.1.2 |
| [Maxwell the Cat](https://thunderstore.io/c/valheim/p/trustworthy/MaxwellTheCat/)                                                | 0.0.1 |
| [Mining](https://thunderstore.io/c/valheim/p/Smoothbrain/Mining/)                                                                | 1.1.7 |
| [Missing Pieces](https://thunderstore.io/c/valheim/p/BentoG/MissingPieces/)                                                      | 2.3.2 |
| [More World Locations AIO](https://thunderstore.io/c/valheim/p/warpalicious/More_World_Locations_AIO/)                           | 5.1.9 |
| [Move Build Pieces](https://thunderstore.io/c/valheim/p/DragonMotion/MoveBuildPieces/)                                           | 1.1.1 |
| [My Little UI](https://thunderstore.io/c/valheim/p/shudnal/MyLittleUI/)                                                          | 1.2.26 |
| [Network Performance System](https://thunderstore.io/c/valheim/p/MidnightMods/NetworkPerformanceSystem/)                         | 1.15.1 |
| [Odin's Steelworks](https://thunderstore.io/c/valheim/p/OdinPlus/OdinsSteelworks/)                                               | 0.4.1 |
| [Odin’s Food Barrels](https://thunderstore.io/c/valheim/p/OdinPlus/OdinsFoodBarrels/) | 1.4.0 |
| [Ore Mines](https://thunderstore.io/c/valheim/p/blacks7ar/OreMines/)                                                             | 1.2.1 |
| [Passive Powers](https://thunderstore.io/c/valheim/p/Smoothbrain/PassivePowers/)                                                 | 1.1.5 |
| [Perfect Placement](https://thunderstore.io/c/valheim/p/Azumatt/PerfectPlacement/)                                               | 1.2.2 |
| [Plant Easily](https://thunderstore.io/c/valheim/p/Advize/PlantEasily/)                                                          | 2.3.0 |
| [Plant Everything](https://thunderstore.io/c/valheim/p/Advize/PlantEverything/)                                                  | 1.21.3 |
| [Portal Lines](https://thunderstore.io/c/valheim/p/Jumpingmushroom/PortalLines/)                                                 | 0.8.1 |
| [Portal Preview](https://thunderstore.io/c/valheim/p/Ivvty/PortalPreview/) | 1.3.6 |
| [Quick Stack Store Sort Trash Restock](https://thunderstore.io/c/valheim/p/Goldenrevolver/Quick_Stack_Store_Sort_Trash_Restock/) | 1.4.15 |
| [Quick Teleport](https://thunderstore.io/c/valheim/p/OdinPlus/QuickTeleport/)                                                    | 2.0.4 |
| [Recipe Description Expansion](https://thunderstore.io/c/valheim/p/Azumatt/Recipe_Description_Expansion/)                        | 1.1.9 |
| [Recipe Sync](https://thunderstore.io/c/valheim/p/Rock3t/RecipeSync/)                                                            | 1.0.0 |
| [Recycle N Reclaim](https://thunderstore.io/c/valheim/p/Azumatt/Recycle_N_Reclaim/)                                              | 1.4.5 |
| [Renegade Vikings](https://thunderstore.io/c/valheim/p/blacks7ar/RenegadeVikings/)                                               | 1.4.2 |
| [Sea Animals](https://thunderstore.io/c/valheim/p/Marlthon/SeaAnimals/)                                                          | 0.3.9 |
| [Seed Bed](https://thunderstore.io/c/valheim/p/blacks7ar/SeedBed/)                                                               | 1.2.9 |
| [Server devcommands](https://thunderstore.io/c/valheim/p/JereKuusela/Server_devcommands/)                                        | 1.115.0 |
| [Sleep Skip](https://thunderstore.io/c/valheim/p/Azumatt/SleepSkip/)                                                             | 1.3.2 |
| [Smart Wishbone Updated](https://thunderstore.io/c/valheim/p/VerdantsAscent/SmartWishboneUpdated/)                               | 1.0.6 |
| [Smelter Upgrades](https://thunderstore.io/c/valheim/p/OverDrive/SmelterUpgrades/)                                               | 1.1.5 |
| [SNEAKer](https://thunderstore.io/c/valheim/p/blacks7ar/SNEAKer/)                                                                | 1.1.8 |
| [Social System](https://thunderstore.io/c/valheim/p/M2Valheim/SocialSystem/)                                                     | 1.0.4 |
| [Solid Hitboxes](https://thunderstore.io/c/valheim/p/Korppis/SolidHitboxes/)                                                     | 1.0.8 |
| [Spearfishing](https://thunderstore.io/c/valheim/p/Korppis/Spearfishing/)                                                        | 1.0.5 |
| [Speedy Paths](https://thunderstore.io/c/valheim/p/Nextek/SpeedyPaths/)                                                          | 1.0.9 |
| [Stamina Regeneration From Food](https://thunderstore.io/c/valheim/p/Smoothbrain/StaminaRegenerationFromFood/)                   | 1.5.8 |
| [Structure Tweaks](https://thunderstore.io/c/valheim/p/JereKuusela/Structure_Tweaks/)                                            | 1.37.0 |
| [Take All Cooked](https://thunderstore.io/c/valheim/p/hoskope/TakeAllCooked/)                                                    | 1.1.0 |
| [Torches Are Fires](https://thunderstore.io/c/valheim/p/blacks7ar/TorchesAreFires/)                                              | 1.1.0 |
| [Traders Extended](https://thunderstore.io/c/valheim/p/shudnal/TradersExtended/)                                                 | 2.0.5 |
| [Transmog](https://thunderstore.io/c/valheim/p/Alpus/Transmog/)                                                                  | 3.2.0 |
| [Upgrade World](https://thunderstore.io/c/valheim/p/JereKuusela/Upgrade_World/) | 1.84.0 |
| [Useful Runestones](https://thunderstore.io/c/valheim/p/sighsorry/UsefulRunestones/)                                             | 1.0.3 |
| [Valheim Community Patch](https://thunderstore.io/c/valheim/p/MidnightMods/ValheimCommunityPatch/) | 0.33.0 |
| [Valheim Infinite Fire](https://thunderstore.io/c/valheim/p/MidnightMods/ValheimInfiniteFire/)                                   | 1.4.0 |
| [Valheim Visual Enhanced](https://thunderstore.io/c/valheim/p/Pumpkin/ValheimVisualEnhanced/)                                    | 0.5.18 |
| [Viking NPC Continued](https://thunderstore.io/c/valheim/p/SephrinMods/VikingNPC_Continued/)                                     | 0.4.1 |
| [VNEI](https://thunderstore.io/c/valheim/p/MSchmoecker/VNEI/)                                                                    | 0.17.6 |
| [Wacky's Database](https://thunderstore.io/c/valheim/p/WackyMole/WackysDatabase/)                                                | 2.5.39 |
| [Warfare](https://thunderstore.io/c/valheim/p/Therzie/Warfare/)                                                                  | 1.9.4 |
| [Wield Equipment While Swimming](https://thunderstore.io/c/valheim/p/blacks7ar/WieldEquipmentWhileSwimming/)                     | 1.1.4 |
| [World Edit Commands](https://thunderstore.io/c/valheim/p/JereKuusela/World_Edit_Commands/)                                      | 1.80.0 |
| [XPortal](https://thunderstore.io/c/valheim/p/SpikeHimself/XPortal/)                                                             | 1.2.25 |

Кроме модов, пакет содержит звуковые субтитры Valheim **1.0.17**.

## Настройки

Обычно менять настройки не требуется. Файл `BepInEx/config/muratovnik.tolmach.cfg` создаётся после первого запуска. Закройте игру перед редактированием; изменения применятся при следующем запуске.

| Задача                                             | Что изменить                                                                                            |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Отключить Tolmach                                  | В секции `[General]` задать `Enabled = false`.                                                          |
| Отключить перевод одного мода                      | В секции `[Modules]` задать `false` напротив его идентификатора. Остальные переводы останутся включены. |
| Оставить стандартные подписи карты в исходном виде | В секции `[Display]` задать `TranslateStandardMapLabels = false`.                                       |

Для диагностики совместимости есть отдельный параметр: `[Compatibility]` → `OnlyAuditedVersions = true`. Он отключает адресные переводы интерфейса для версий, отличающихся от каталога; переводы через словари остаются. По умолчанию параметр выключен.

## Совместимость и ограничения

Готовые фразы сопоставляются по тексту. Пользовательская надпись, полностью совпавшая с такой фразой, тоже может отображаться переведённой — сохранённое значение не меняется. Это относится и к стандартным подписям карты, например `Camp`. Широкие шаблоны имён Humanoid Randomizer применяются отдельно, только к стандартным именам существ, принадлежность которых моду подтверждена. Это включает созданные модом варианты и приручаемых существ; заданное игроком имя питомца сохраняется.

Стандартные имена NPC Viking NPC Continued переводятся только у неприручённых NPC. У приручённых имя сохраняется, даже если оно было выдано автоматически: оно хранится в том же поле, что и имя, введённое игроком.

Интерфейс **Configuration Manager** переведён; названия разделов и параметров других модов в F1 в общем случае остаются как есть. Поддерживаемые настройки **Creature Level and Loot Control** — исключение. Имена секций и ключей конфигурации не меняются.

## Помощь

### Перевод не появился

Проверьте, что Tolmach включён в том же профиле, что и нужный мод, в игре выбран русский язык и игра перезапущена после его смены. Затем сверьтесь с [покрытием](https://github.com/Muratovnik/tolmach/blob/main/docs/COVERAGE.md): отдельный экран может не входить в перевод.

Для диагностики откройте `BepInEx/config/Tolmach.runtime.txt` в папке профиля. Проверьте время и идентификатор `Session` по журналу текущего запуска. `Status: Active` означает, что инициализация завершилась, а `Russian selected: True` — что определён русский язык. Это не подтверждает перевод каждого экрана: ниже указаны найденные модули и предупреждения известных адаптеров.

`Disabled`, `MissingCatalog` и `Failed` означают соответственно отключение в конфиге, отсутствие каталога переводов и ошибку запуска. Если Tolmach отключён в менеджере или не загрузился, он не может обновить отчёт — на диске может остаться старый файл. [Подробнее об отчёте](https://github.com/Muratovnik/tolmach/blob/main/docs/INSTALLATION.md#диагностика).

### Ошибка или неудачная формулировка

[Сообщить об ошибке перевода](https://github.com/Muratovnik/tolmach/issues/new). Укажите мод и его версию, где встречается текст, и приложите скриншот. Вместо скриншота можно скопировать строку целиком. Отчёт Tolmach можно приложить дополнительно для диагностики; он не обязателен для сообщения о формулировке.

## Благодарности и лицензия

«Толмач» — старинное слово со значением «переводчик». Пакет вдохновлён [ObeliskRU](https://thunderstore.io/c/valheim/p/DragonMotion/ObeliskRU/).

[Лицензия MIT](https://github.com/Muratovnik/tolmach/blob/main/LICENSE).
