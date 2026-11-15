# Сборка и обновление

Собираю PackHelper на Windows 10 или 11 с .NET Framework 4.8.

Из папки проекта:

```powershell
.\test.ps1
.\build.ps1 -PackPath '.\LavaResurs.zip' -PackVersion '1.11.6'
```

Готовый EXE, данные пака и SHA-256 появятся в `dist`. Сборка проверяет, что в корне ZIP есть `pack.mcmeta`.

При обновлении использую точный ZIP, который раздаёт сервер. `pack.id` и SHA-1 в конфиге PackChecker должны совпадать с `pack-release.json` из сборки. ZIP нельзя перепаковывать после вычисления хеша.

В выпуск прикладываю EXE, `pack-release.json` и `SHA256SUMS.txt`. Системный .NET Framework компилирует код без загрузки дополнительных пакетов.

Проверки запускаются и в GitHub Actions. Полную установку и вход проверяю отдельно в Minecraft.
