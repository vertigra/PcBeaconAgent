# PcBeaconAgentService <img src=".github/assets/beacon.png" align="right" height="50" alt="PcBeaconAgent Logo">

[![Release Status](https://github.com/vertigra/PcBeaconAgentService/actions/workflows/release.yml/badge.svg)](https://github.com/vertigra/PcBeaconAgentService/actions/workflows/release.yml)

---

### Описание
Фоновая служба Windows (`BackgroundService`) на платформе **.NET 10** для мониторинга состояния ПК и отправки регулярных сигналов (беконов) на управляющий сервер.

### Особенности сборки
* **Single-File Executable**: проект собирается в один самодостаточный `.exe` файл.
* **Trimmed**: весь неиспользуемый код вырезается при компиляции для оптимизации размера.
* **Self-Contained**: рантайм .NET 10 упакован внутрь, установка дополнительного ПО на целевой машине не требуется.

### Развертывание
При каждом пуше тега формата `v.*` (например, `v.0.0.1`) автоматически запускается GitHub Action, который компилирует службу и публикует готовый релизный `zip`-архив, содержащий:
1. `PcBeaconAgent.exe` — исполняемый файл службы.
2. `appsettings.json` — конфигурационный файл настроек.