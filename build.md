# Инструкция по сборке и запуску (Linux & NixOS)

discord-nowcast для Linux позволяет отображать текущий трек из любого медиаплеера Linux (через DBus MPRIS2) и напрямую из аккаунта **Яндекс Музыки** (через API очередей и истории Ynison) в статусе Discord Rich Presence.

---

## 1. Сборка через Nix (`nix build`)

Проект полностью настроен для Nix и NixOS с поддержкой flakes и классического `nix-build`.

### С Flakes:
```bash
# Сборка бинарника в ./result/bin/discord-nowcast
nix build

# Проверка версии собранного пакета
./result/bin/discord-nowcast --version

# Запуск прямо через nix run
nix run . -- --status
```

### Без Flakes (nix-build):
```bash
nix-build default.nix
./result/bin/discord-nowcast --version
```

---

## 2. Подключение аккаунта Яндекс Музыки

Для пользователей, у которых Яндекс Музыка играет в мобильном приложении, браузере или на умной колонке (Яндекс Станция), discord-nowcast умеет синхронизировать статус напрямую через аккаунт Яндекса.

### Интерактивная настройка:
Запустите команду:
```bash
./result/bin/discord-nowcast --yandex-login
```
Программа запросит ваш OAuth-токен Яндекса и сохранит его в `~/.config/discord-nowcast/config.json`.

### Как получить OAuth-токен:
1. Перейдите по ссылке авторизации Яндекса:
   `https://oauth.yandex.ru/authorize?response_type=token&client_id=23cabbbdc6cd418abb4b46c32ba3e112`
2. Разрешите доступ и скопируйте полученный токен из адресной строки (`access_token=...`).
3. Также вы можете вручную вписать токен в файл `~/.config/discord-nowcast/config.json`:
   ```json
   {
     "yandexToken": "ВАШ_ТОКЕН_ЗДЕСЬ"
   }
   ```

---

## 3. Запуск и управление

### Проверить текущий статус и видимые плееры:
```bash
discord-nowcast --status
```

### Запуск демона в фоне / терминале:
```bash
discord-nowcast --daemon
```

### Добавить в автозапуск (systemd / XDG autostart):
```bash
# Включить автозапуск
discord-nowcast --autostart-enable

# Отключить автозапуск
discord-nowcast --autostart-disable
```

### Просмотр логов:
```bash
discord-nowcast --log
# или напрямую:
tail -f ~/.config/discord-nowcast/log.txt
```

---

## 4. Использование в NixOS Configuration

Вы можете добавить discord-nowcast в свою конфигурацию NixOS (`configuration.nix`) или Home Manager:

### Home Manager:
```nix
{ pkgs, ... }:
let
  discord-nowcast = pkgs.callPackage /path/to/discord-nowcast-linux/package.nix {};
in {
  home.packages = [ discord-nowcast ];

  systemd.user.services.discord-nowcast = {
    Unit = {
      Description = "discord-nowcast Rich Presence";
      After = [ "graphical-session.target" ];
    };
    Service = {
      ExecStart = "${discord-nowcast}/bin/discord-nowcast --daemon";
      Restart = "on-failure";
    };
    Install = {
      WantedBy = [ "default.target" ];
    };
  };
}
```
