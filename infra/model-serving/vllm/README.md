# Gemma 4 на локальном vLLM

Инструкция описывает настройку тестового окружения на Windows-компьютере с
NVIDIA RTX 4060 8 GB. vLLM запускается внутри WSL2/Ubuntu 24.04, а наружу
выдаёт OpenAI-compatible API через Windows portproxy.

## Итоговая схема

```text
Клиент / C#-агент
        |
        | http://192.168.19.120:8000/v1
        v
Windows: portproxy + Firewall
        |
        v
WSL2: Ubuntu-24.04
        |
        v
systemd -> vllm.service -> Gemma 4
```

Параметры работающей конфигурации:

| Параметр | Значение |
|---|---|
| Windows-хост | `192.168.19.120` |
| Дистрибутив | `Ubuntu-24.04`, WSL2 |
| GPU | NVIDIA RTX 4060, 8 GB |
| vLLM | nightly CUDA 12.9 wheel |
| Модель | `google/gemma-4-E4B-it-qat-w4a16-ct` |
| Имя модели в API | `gemma-4-e4b-it` |
| API | `http://192.168.19.120:8000/v1` |
| Контекст | 4096 токенов |
| Параллельность | 2 последовательности |
| Режим | текстовый; image/audio/video отключены |

Модель является официальным 4-битным server checkpoint Gemma 4. Для RTX 4060
8 GB используются CPU offload 5 GB и фиксированный KV-cache 512 MiB. Это
позволяет загрузить модель в тестовой конфигурации, но скорость ниже, чем на
GPU с большим объёмом VRAM.

## Файлы в этой папке

- `install.sh` — устанавливает системные зависимости, CUDA 12.9 compiler,
  Python 3.12, uv и nightly vLLM.
- `model-config.example.env` — пример переменных конфигурации модели.
- `vllm.service` — systemd-служба OpenAI-compatible API.
- `configure-portproxy.ps1` — находит текущий IP WSL, настраивает Windows
  portproxy и правило Firewall.
- `start-vllm-wsl.ps1` — скрипт автозапуска: обновляет portproxy, запускает
  службу vLLM и удерживает WSL запущенным.

## 1. Подготовка Windows и WSL2

Команды выполняются в PowerShell от имени администратора.

На чистой системе достаточно установить WSL2 и Ubuntu 24.04:

```powershell
wsl --install --no-distribution
wsl --set-default-version 2
wsl --install -d Ubuntu-24.04
wsl -l -v
```

В результате дистрибутив должен иметь состояние `Running` или `Stopped` и
версию `2`:

```text
  NAME            STATE           VERSION
* Ubuntu-24.04    Stopped         2
```

На исходном тестовом хосте регистрация WSL была повреждена (`REGDB_E_CLASSNOTREG`).
Для восстановления были включены компоненты `VirtualMachinePlatform` и
`Microsoft-Windows-Subsystem-Linux`, установлен официальный WSL MSI, после
чего Ubuntu 24.04 была импортирована как WSL2-дистрибутив. При аналогичной
ошибке сначала выполните:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -All -NoRestart
```

Перезагрузите Windows и повторите установку/импорт Ubuntu. Не устанавливайте
Linux NVIDIA driver внутри WSL: используется драйвер NVIDIA Windows с CUDA
поддержкой WSL.

Проверьте GPU из WSL:

```powershell
wsl -d Ubuntu-24.04 -- nvidia-smi
```

## 2. Установка vLLM внутри Ubuntu

Скопируйте содержимое этой папки в WSL или клонируйте проект, затем выполните:

```bash
cd /path/to/vllm
sudo bash install.sh
```

Скрипт устанавливает:

1. Python 3.12, `python3.12-venv`, `build-essential` и `ninja-build`.
2. Минимальные CUDA 12.9 compiler и `libcurand-dev-12-9`, необходимые для
   сборки/запуска некоторых vLLM-компонентов.
3. `uv` и отдельное виртуальное окружение `/opt/vllm`.
4. Предварительную nightly-версию vLLM с CUDA 12.9 и PyTorch CUDA 12.9.
5. Каталоги `/etc/vllm`, `/var/lib/vllm`, `/var/cache/huggingface`.
6. Официальный chat template Gemma 4 в
   `/etc/vllm/tool_chat_template_gemma4.jinja`.

Проверка установки:

```bash
/opt/vllm/bin/vllm --version
/opt/vllm/bin/python -c "import torch; print(torch.__version__, torch.cuda.is_available())"
nvidia-smi
```

В исходной установке были проверены vLLM
`0.26.1rc1.dev403+g821717118`, PyTorch `2.13.0+cu129` и доступность CUDA.

## 3. Конфигурация модели

Создайте рабочий файл конфигурации из примера:

```bash
sudo install -d -m 0755 /etc/vllm /var/cache/huggingface
sudo cp model-config.example.env /etc/vllm/vllm.env
sudo chmod 600 /etc/vllm/vllm.env
sudo nano /etc/vllm/vllm.env
```

Минимальное содержимое:

```dotenv
MODEL_ID=google/gemma-4-E4B-it-qat-w4a16-ct
SERVED_MODEL_NAME=gemma-4-e4b-it
VLLM_PORT=8000
VLLM_API_KEY=replace-with-a-long-random-value
MAX_MODEL_LEN=4096
GPU_MEMORY_UTILIZATION=0.82
CPU_OFFLOAD_GB=5
KV_CACHE_MEMORY_BYTES=536870912
MAX_NUM_SEQS=2
```

API key должен быть длинным случайным значением. Он хранится только в
`/etc/vllm/vllm.env` с правами `600`; не добавляйте его в git и не вставляйте
в исходный код клиента.

## 4. Установка и запуск systemd-службы

Скопируйте unit-файл:

```bash
sudo cp vllm.service /etc/systemd/system/vllm.service
sudo systemctl daemon-reload
sudo systemctl enable --now vllm
```

Проверка:

```bash
sudo systemctl is-enabled vllm
sudo systemctl is-active vllm
sudo systemctl status vllm --no-pager
sudo journalctl -u vllm -n 100 --no-pager
```

В unit-файле есть несколько важных параметров:

- `VLLM_USE_V2_MODEL_RUNNER=0` — отключает V2 runner, который в WSL на этой
  конфигурации упирался в ошибку UVA.
- `--enforce-eager` — отключает проблемные для данного WSL/GPU AOT-графы.
- `--cpu-offload-gb 5` — выгружает часть весов в оперативную память Windows/WSL.
- `--kv-cache-memory-bytes 536870912` — ограничивает KV-cache 512 MiB, чтобы
  модель не занимала всю физическую память RTX 4060.
- `--limit-mm-per-prompt '{"image":0,"audio":0,"video":0}'` — текущий
  профиль текстовый и не принимает мультимодальные входы.
- `--enable-auto-tool-choice`, `--tool-call-parser gemma4` и
  `--reasoning-parser gemma4` — включают tool calling и разбор reasoning Gemma 4.

Первый запуск скачивает модель в `/var/cache/huggingface` и может занять
несколько минут.

## 5. Доступ к API из Windows и сети

IP WSL меняется после перезапуска WSL, поэтому нельзя один раз навсегда
прописать его в portproxy. Скрипт `configure-portproxy.ps1` каждый раз находит
актуальный IP дистрибутива и создаёт правило:

```powershell
.\configure-portproxy.ps1
```

Скрипт настраивает:

- `0.0.0.0:8000` → текущий IP WSL `:8000`;
- входящее правило Windows Firewall `vLLM OpenAI API` для TCP/8000.

Проверка правила:

```powershell
netsh interface portproxy show v4tov4
Get-NetFirewallRule -DisplayName 'vLLM OpenAI API'
```

Ожидается portproxy вида:

```text
0.0.0.0         8000        <WSL-IP>        8000
```

## 6. Автозапуск после перезагрузки Windows

На тестовом хосте создана задача Планировщика Windows с именем
`vLLM Gemma 4`. Она запускается при старте Windows, вызывает
`start-vllm-wsl.ps1`, обновляет portproxy, запускает `systemctl start vllm` и
удерживает WSL-процесс активным.

Проверка задачи:

```powershell
Get-ScheduledTask -TaskName 'vLLM Gemma 4' |
  Select-Object TaskName, State
Get-ScheduledTaskInfo -TaskName 'vLLM Gemma 4' |
  Select-Object LastRunTime, LastTaskResult
```

После перезагрузки Windows проверьте, что задача имеет состояние `Running`, а
API отвечает. Если задача создаётся вручную, запускать её нужно от имени
пользователя, у которого есть доступ к WSL, с правами `Highest`.

## 7. Проверка API

Проверка списка моделей из Windows:

```powershell
$key = Get-Content 'C:\Users\kos4v\vllm-api-key.txt'
curl.exe http://192.168.19.120:8000/v1/models `
  -H "Authorization: Bearer $key"
```

Проверка health endpoint:

```powershell
curl.exe -i http://192.168.19.120:8000/health
```

Тест генерации:

```powershell
$body = @{
  model = 'gemma-4-e4b-it'
  messages = @(
    @{ role = 'user'; content = 'Ответь одним словом: столица Франции?' }
  )
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Uri 'http://192.168.19.120:8000/v1/chat/completions' `
  -Method Post `
  -Headers @{ Authorization = "Bearer $key" } `
  -ContentType 'application/json' `
  -Body $body
```

В рабочем тесте модель ответила `Paris`. Tool calling также проверен: Gemma 4
вернула вызов функции с именем и JSON-аргументами.

## 8. Подключение C#-агента

Для проекта на `Microsoft.Agents.AI` используется OpenAI SDK с кастомным
endpoint. В `Program.cs` задаются переменные:

```powershell
$env:VLLM_BASE_URL = 'http://192.168.19.120:8000/v1'
$env:VLLM_MODEL = 'gemma-4-e4b-it'
$env:VLLM_API_KEY = '<ключ из vllm-api-key.txt>'
dotnet run --project .\sandbox\first_agent\first_agent.csproj
```

Важно: endpoint должен заканчиваться на `/v1`, а API key должен совпадать со
значением `VLLM_API_KEY` на сервере.

## 9. Управление и диагностика

Перезапуск после изменения `/etc/vllm/vllm.env`:

```bash
sudo systemctl daemon-reload
sudo systemctl restart vllm
sudo journalctl -fu vllm
```

Проверка из PowerShell:

```powershell
wsl -d Ubuntu-24.04 -u root -- systemctl is-active vllm
wsl -d Ubuntu-24.04 -u root -- journalctl -u vllm -n 100 --no-pager
wsl -d Ubuntu-24.04 -u root -- nvidia-smi
```

Если после перезапуска WSL API недоступен:

```powershell
wsl -d Ubuntu-24.04 -u root -- systemctl restart vllm
.\configure-portproxy.ps1
netsh interface portproxy show v4tov4
```

Если требуется полностью остановить окружение:

```powershell
wsl -d Ubuntu-24.04 -u root -- systemctl stop vllm
wsl --shutdown
```

## 10. Безопасность

- Не публикуйте `VLLM_API_KEY` в репозитории, логах и сообщениях.
- Правило Firewall открывает TCP/8000 для профилей Windows `Any`; ограничьте
  `RemoteAddress`, если API не должен быть доступен всей локальной сети.
- Для production нужен HTTPS reverse proxy, отдельная сеть и ротация ключей.
- В текущей тестовой конфигурации API рассчитан на доверенную локальную сеть.
