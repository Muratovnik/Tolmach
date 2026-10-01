# Изменения

## [0.7.0](https://github.com/Muratovnik/Tolmach/compare/v0.6.2...v0.7.0) (2026-10-01)

### Highlights

Добавлен перевод интерфейса Configuration Manager, причин отказа подключения Conditional Config Sync и уведомления Async Save. Новые строки Social System, More World Locations и Wacky's Database переведены по текущим версиям. Пакет содержит переводы и дополнения для 90 модов.

### Features

- **Настройки:** переведены поиск, редактор значений и файлов, кнопки, подсказки синхронизации и кнопка главного меню Configuration Manager 1.1.22; сохранённые значения конфигурации не меняются. ([53a71b2](https://github.com/Muratovnik/Tolmach/commit/53a71b25015b97e8eab3a956b267e9dfe621675a))
- **Подключение:** переведены причины отказа Conditional Config Sync 1.0.9 и рекомендации по восстановлению подключения; несколько причин обрабатываются построчно с сохранением имён модов и версий. ([53a71b2](https://github.com/Muratovnik/Tolmach/commit/53a71b25015b97e8eab3a956b267e9dfe621675a))
- **Сообщения:** добавлены уведомление Async Save 0.6.0, сообщения синхронизации ресурсов и ошибки подключения Wacky's Database 2.5.38, заголовки раскрытия данных Adventure Backpacks 2.2.5. ([53a71b2](https://github.com/Muratovnik/Tolmach/commit/53a71b25015b97e8eab3a956b267e9dfe621675a))
- **Совместимость:** каталоги обновлены для Social System 1.0.2, More World Locations AIO 5.1.7, Wacky's Database 2.5.38, Network Performance System 1.13.0 и Adventure Backpacks 2.2.5. Список поддерживаемых пакетов и версий согласован с каталогами. ([53a71b2](https://github.com/Muratovnik/Tolmach/commit/53a71b25015b97e8eab3a956b267e9dfe621675a))

## [0.6.2](https://github.com/Muratovnik/Tolmach/compare/v0.6.1...v0.6.2) (2026-10-01)

### Highlights

Уменьшена задержка при первом выборе рецепта брони с бонусом комплекта. В описаниях Recipe Description Expansion части комплектов показывают игровые русские названия и при переносах строк Windows. Обновлены каталоги для пяти новых версий модов.

### Bug Fixes

- **Крафт:** уменьшена стоимость первого поиска названий предметов и комплектов; после регистрации новых предметов описания обновляются. ([e369f86](https://github.com/Muratovnik/Tolmach/commit/e369f86345102c71f9c097556370c6c2bb241f8e))
- **Комплекты:** исправлен перевод частей в описаниях Recipe Description Expansion с переносами строк LF и CRLF; цвета, отметки и переносы сохраняются. ([e369f86](https://github.com/Muratovnik/Tolmach/commit/e369f86345102c71f9c097556370c6c2bb241f8e))
- **Совместимость:** каталоги сверены с Humanoid Randomizer 1.6.1, Infinity Hammer 1.86.0, World Edit Commands 1.79.0, Network Performance System 1.11.1 и Adventure Backpacks 2.2.4. ([e369f86](https://github.com/Muratovnik/Tolmach/commit/e369f86345102c71f9c097556370c6c2bb241f8e))

## [0.6.1](https://github.com/Muratovnik/Tolmach/compare/v0.6.0...v0.6.1) (2026-09-30)

### Highlights

Исправлены переводы настроек Creature Level and Loot Control, составных подсказок и имён вариантов Humanoid Randomizer. Готовые русские тексты и пользовательские имена питомцев сохраняются. Отчёт загрузки теперь помогает отличить текущий запуск от старого файла на диске.

Обновлены описание пакета, установка, помощь и справочник покрытия. Уточнены ограничения перевода пользовательских надписей, настройки F1 и диагностика. Финальная приёмка с загрузкой мира ещё не выполнена; [границы автоматических проверок](https://github.com/Muratovnik/Tolmach/blob/v0.6.1/docs/VALIDATION.md) остаются в силе.

### Bug Fixes

- **CLLC:** названия и описания настроек дополняются независимо: готовый русский текст одного поля не мешает перевести другое и не перезаписывается. ([a3f7e73](https://github.com/Muratovnik/Tolmach/commit/a3f7e73658acacfe262be47b53eaa6953b87db49))
- **Подсказки:** фрагменты нескольких каталогов переводятся в одном сообщении без повторной обработки уже переведённого текста. ([fbf3651](https://github.com/Muratovnik/Tolmach/commit/fbf365134895711b750fae23aea8b994bd37780b))
- **Humanoid Randomizer:** шаблоны имён ограничены существами этого мода, включая варианты без компонента рандомизатора и стандартные имена приручаемых существ. Пользовательские переименования сохраняются. При неизвестном игровом API имя остаётся без глобального запасного перевода. ([2f3bed0](https://github.com/Muratovnik/Tolmach/commit/2f3bed0ae6be2b5db87770c4d57ffe59f1a1e011))
- **Диагностика:** отчёт различает запуск, отключение, отсутствие каталогов, ошибку и завершение работы; идентификатор сеанса связывает его с текущим журналом. ([f357bd1](https://github.com/Muratovnik/Tolmach/commit/f357bd16f2147defc9ca45bb6b362e884fdefe76))

## [0.6.0](https://github.com/Muratovnik/Tolmach/compare/v0.2.0...v0.6.0) (2026-09-29)

### Highlights

Первый выпуск на Thunderstore. После 0.2.0 пакет расширен с 37 до **87 модов** и дополнен 142 звуковыми субтитрами Valheim 1.0.16. Другой русификатор не является обязательной зависимостью. Объём перевода различается: от основных окон и предметов до отдельных сообщений о несовпадении версий.

**Новые переводы.** Добавлены Creature Level and Loot Control, Social System, Armory от Therzie, моды Balrond, Odin's Steelworks, VNEI, Inventory Slots и другие. CLLC получил русские названия существ с эффектами, сообщения и настройки F1; Social System — друзей, группы, приглашения, личные сообщения и проверку готовности. [Полный список и покрытие](docs/COVERAGE.md).

**Предметы и подсказки.** Расширены Shipyard, More World Locations, Plant Everything, Azu Auto Store и Azu Crafty Boxes. Recipe Description Expansion показывает части и названия комплектов как в игре, вместо внутренних идентификаторов. В Equipment and Quick Slots переведены «Плечи» и «Оберег», в Take All Cooked — стандартная подсказка «Взять все готовое». В Missing Pieces исправлены три строки собственного русского перевода.

**Термины.** Названия согласованы с предметами и контекстом модов, а не только с одинаковыми английскими словами. В VNEI остаются «Рецепты» и «Все станции», в Gold Bars — «Небольшая кучка монет», в Shipyard — «Просмоленная лестница 2 м», в Viking NPC — «Хозяин». Сохранены написание и обозначения единиц, принятые в локализации игры.

**Совместимость.** Несовпадение версии мода больше не отключает все его адресные переводы по умолчанию: Tolmach пробует известные строки. Режим `Compatibility.OnlyAuditedVersions = true` оставляет для других версий только словарные переводы. Старую настройку `AllowOtherVersions` можно удалить. После смены языка перезапустите игру: некоторые моды создают подписи при запуске.

**Границы выпуска.** Не переведены консольная справка инструментов JereKuusela, технические имена инструментов Infinity Hammer и окно ConditionalConfigSync. Наличие каталога не подтверждает каждый экран: игровая приёмка финальной 0.6.0 в сохранённых протоколах не отмечена выполненной. [Проверки и их ограничения](docs/VALIDATION.md).

### Features

- Добавлены Social System и ещё 18 каталогов; исправлены строки Missing Pieces и названия комплектов Recipe Description Expansion. ([ea3e9ac](https://github.com/Muratovnik/Tolmach/commit/ea3e9acb683610cc2a08d3ba6ac0f09e4394291e))
- Адресные адаптеры могут работать на версиях модов, отличающихся от проверенных. ([ea3e9ac](https://github.com/Muratovnik/Tolmach/commit/ea3e9acb683610cc2a08d3ba6ac0f09e4394291e))
- Добавлены таблица и адаптер Creature Level and Loot Control. ([a445acb](https://github.com/Muratovnik/Tolmach/commit/a445acb859b9be2d236c1a50f3268b345969c7c2))
- Добавлены 25 каталогов модов, включая Balrond, VNEI, Odin's Steelworks и Inventory Slots. ([09ab47d](https://github.com/Muratovnik/Tolmach/commit/09ab47d2a158376cbf1beac7a5e796624ed34ed7))
- Добавлены пять каталогов, звуковые субтитры игры и перевод строк, которые моды хранят в объектах. ([66b5328](https://github.com/Muratovnik/Tolmach/commit/66b5328288b0237753a84558834e0280c67df57f))
- Добавлен перевод стандартного текста настройки в месте его показа, без изменения файла конфигурации. ([7c8df24](https://github.com/Muratovnik/Tolmach/commit/7c8df24445da7e62202011e8af17ce923e6abe56))
- Загрузчик и валидатор отклоняют каталоги с неправильными типами и противоречащими переводами. ([f48a3d5](https://github.com/Muratovnik/Tolmach/commit/f48a3d5871023cd5a67846e5694375b3efebd2c8)) ([87813bd](https://github.com/Muratovnik/Tolmach/commit/87813bd7bdf52ecf8ce9328a61fc519415db18eb))

### Bug Fixes

- Методы с неподдерживаемыми обработчиками исключений не пересобираются Harmony; доступные вызовы показа переводятся отдельно с проверкой вызывающего метода. ([ea3e9ac](https://github.com/Muratovnik/Tolmach/commit/ea3e9acb683610cc2a08d3ba6ac0f09e4394291e))
- У перегруженных методов патчится только тело, содержащее нужную строку. ([5145688](https://github.com/Muratovnik/Tolmach/commit/5145688530e0f4967f8e63facf61a1c245ac49fa))
- Исправлены переводы предметов, для которых одинаковое английское слово обозначало разные объекты. ([c1ecc3b](https://github.com/Muratovnik/Tolmach/commit/c1ecc3b939bcc7e4fdac4ecb3666d5f6cf8c88d5))
- Восстановлены пропущенные строки и исправлены подписи, найденные при проверке ранней сборки в игре. ([0a2c5df](https://github.com/Muratovnik/Tolmach/commit/0a2c5dffac0c6d047a94108bc97527b717c2c41a)) ([22cb6a4](https://github.com/Muratovnik/Tolmach/commit/22cb6a480f91d3c721def5739b8108553c62749f))
- Каталоги получили префикс `tolmach-`, чтобы встроенные менеджеры локализации других модов не принимали их за собственные файлы. ([69dd148](https://github.com/Muratovnik/Tolmach/commit/69dd148e8130679c27329439d7659aed4ce74500))
- Проверка пакета декодирует иконку, а не доверяет только заголовку PNG. ([d585efd](https://github.com/Muratovnik/Tolmach/commit/d585efd9b33d247c00728506826e54aeae0f7752))

## 0.2.0 — новое имя: Tolmach

Первый публичный выпуск под именем **Tolmach**. Переводы и поведение 37 каталогов не менялись относительно локальной 0.1.3.

Идентификатор плагина — `muratovnik.tolmach`, DLL — `Tolmach.dll`, настройки — `BepInEx/config/muratovnik.tolmach.cfg`, отчёт — `BepInEx/config/Tolmach.runtime.txt`.

Если установлен прежний локальный **ValheimRussianPack**, удалите его перед установкой Tolmach: идентификаторы разные, и иначе загрузятся оба плагина. Настройки старого пакета автоматически не переносятся.

## История разработки до публичного выпуска

Версии 0.1.x и промежуточные этапы между 0.2.0 и 0.6.0 описаны в [прежней редакции журнала](https://github.com/Muratovnik/Tolmach/blob/446be1f93ca2137f7d64ee0ab6686666418b9992/CHANGELOG.md). Она сохраняет технические детали и отменённые промежуточные решения; окончательный результат 0.6.0 приведён выше.
