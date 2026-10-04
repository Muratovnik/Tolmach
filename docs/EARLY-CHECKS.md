# Проверки до запуска игры

Исходники проверяются компилятором и анализаторами в редакторе и при сборке.
Используйте .NET SDK **10.0.401** с `latestPatch` из `global.json`;
автономным тестам под net9.0 нужен также **.NET Runtime 9**. Целевой framework
плагина и его версия C# заданы в csproj и остаются прежними.

## Что обнаруживается

| Проверка | Что она обнаруживает |
| --- | --- |
| SDK analyzers | Ошибки вроде сравнения упакованных значений через ReferenceEquals, рекурсивного setter и повторного использования ValueTask |
| Microsoft.Unity.Analyzers 1.27.0 | Неверные Unity callbacks, создание Component через new, опасные `?.`, `??` и null patterns для UnityEngine.Object |
| Harmonize 1.0.6 | Отсутствующую или неоднозначную статическую Harmony-цель и неподдерживаемое объявление patch-класса |
| Runtime.Contracts | 6 контрактов методов и полей выбранных оригинальных игровых DLL: точные параметры, return type, static и managed body |

Настройки ошибок находятся в [.editorconfig](../.editorconfig), зависимости —
в [Directory.Build.props](../Directory.Build.props). Анализатор, который не
загрузился или упал, останавливает сборку через AD0001, CS8032 или CS9057.
Советы по стилю и производительности сами по себе не блокируют упаковку.

Harmonize выбран после проверки с HarmonyX из игрового профиля.
HarmonyTools.Analyzers 1.0.16 на этой реализации падал с AD0001;
Harmonize требует Roslyn 5 и поэтому SDK 10. Анализатор проверяет Prefix,
Postfix и Transpiler. Для Finalizer и внешних helper-методов используются
точечные SuppressMessage с причиной; это не подтверждение runtime binding.

## Обычная сборка

Запустите PowerShell из корня репозитория. Для другой установки задайте
реальные `$game` и `$profile`; это каталоги Valheim и профиля BepInEx.

```powershell
$game = 'D:/SteamLibrary/steamapps/common/Valheim'
$profile = "$env:APPDATA/com.kesomannen.gale/valheim/profiles/Default"
./Build.ps1 -GamePath $game -ProfilePath $profile
```

Сборочный сценарий проверяет production plugin, автономные тесты и
[API-контракты](../tests/Runtime.Contracts/contracts.json), затем собирает пакет.
Игра при этих проверках не запускается. Если SDK установлен отдельно от PATH,
передайте скрипту `-DotnetExecutable '<absolute dotnet.exe>'`.

Изменение игры может потребовать пересмотра контрактов. Добавляйте туда важные
API, которые выбираются через строковые имена или reflection, сверяя ожидания
с оригинальной целевой DLL. Контракты не охватывают автоматически весь private API.

## Проверка самих анализаторов

```powershell
./tests/Check-Analyzers.ps1 -Project (Join-Path $PWD 'Tolmach.csproj') `
  -WorkDirectory (Join-Path $PWD 'tmp/analyzer-controls-new') `
  -GamePath $game -ProfilePath $profile
```

Каталог должен быть новым. Проверка собирает правильный пример и три ошибочных
варианта production-проекта. Она требует именно CA2013, UNT0008 и HARMONIZE001,
а не произвольную ошибку сборки. Control constants включаются только для этой
проверки; её DLL нельзя использовать как пакет мода.

## Граница проверки

Компиляция и metadata-контракты не проверяют Unity lifecycle, загрузку ресурсов,
порядок модов, права владельца ZDO, RPC, сохранение и игровой эффект. Для изменений
этих границ используйте соответствующий изолированный игровой сценарий.

Источники: [SDK analyzers](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview),
[Unity analyzers](https://github.com/microsoft/Microsoft.Unity.Analyzers),
[Harmonize](https://github.com/BadMagic100/Harmonize).

