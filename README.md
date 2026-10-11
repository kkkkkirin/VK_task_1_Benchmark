# Black Myth: Wukong Benchmark Tool 

Инструмент на C# для автоматизированного запуска Black Myth: Wukong Benchmark Tool из Steam. Выполняет два прогона бенчмарка — CPU-тест и GPU-тест — с разными наборами настроек, а затем формирует единый отчёт с характеристиками ПК и результатами обоих прогонов.

---

## Возможности

- Автоматический запуск Black Myth: Wukong Benchmark Tool через Steam.
- Автоматическое применение настроек бенчмарка перед каждым прогоном (запись в `GameUserSettings.ini`).
- Автоматизация интерфейса: распознавание элементов экрана через OpenCV (поиск шаблонов) и эмуляция клавиатуры/мыши.
- Ожидание завершения прогона по появлению нового лога в папке истории бенчмарка.
- Копирование логов обоих прогонов в отдельные папки.
- Парсинг JSON-логов и формирование человекочитаемого отчёта.
- Автоматическая резервная копия `GameUserSettings.ini` перед первым изменением.

---

## Требования

- **ОС:** Windows 10/11 (x64).
- **.NET:** 6.0 или выше.
- **Steam** с установленным **Black Myth: Wukong Benchmark Tool**. Бенчмарк должен был запущен хотя бы 1 раз. 
- **NuGet-пакеты:**
  - `OpenCvSharp4` — для распознавания элементов интерфейса.
  - `OpenCvSharp4.runtime.win` — нативные библиотеки OpenCV под Windows.

Установка через NuGet:

```bash
dotnet add package OpenCvSharp4
dotnet add package OpenCvSharp4.runtime.win
```

---

## Структура проекта

```
vk_task1/
│
├── Program.cs                  # Точка входа, разбор аргументов, запуск оркестратора и отчёта
├── BenchmarkOrchestrator.cs    # Оркестрация двух прогонов, ожидание логов, копирование
├── ConfigManager.cs            # Запись настроек в GameUserSettings.ini
├── ScreenAutomation.cs         # Поиск шаблонов на экране, клики и нажатия клавиш
├── BenchmarkLogParser.cs       # Парсинг JSON-логов и формирование отчёта
│
├── Templates/                  # PNG-шаблоны для распознавания интерфейса
│   ├── benchmark-button.png    # Кнопка "Benchmark" в главном меню
│   ├── confirm-benchmark.png   # Окно подтверждения запуска
│   └── benchmark-results.png   # Экран с результатами прогона
│
├── BenchmarkLogs/              # Копии логов прогонов (создаётся автоматически)
│   ├── CPU_20260111_143022/
│   └── GPU_20260111_144512/
│
└── Reports/                    # Итоговые отчёты (создаётся автоматически)
    └── BenchmarkReport.txt
```

---

## Как это работает

1. **Применение конфигурации.**
   `ConfigManager` перезаписывает параметры в `GameUserSettings.ini` бенчмарка согласно текущему профилю (CPU или GPU). Файл предварительно резервируется в `GameUserSettings.ini.backup`.

2. **Снимок папки логов.**
   `BenchmarkOrchestrator` сохраняет состояние папки истории (`%LOCALAPPDATA%\Temp\b1\BenchMarkHistory\Tool`) — имена, размеры и время изменения файлов.

3. **Запуск бенчмарка.**
   Открывается `steam://rungameid/3132990`. Оркестратор ждёт появления окна процесса (`b1_benchmark`, `b1-Win64-Shipping` или `b1`).

4. **Автоматизация интерфейса.**
   - Ждём 40 секунд, пока загрузится приветственный экран.
   - Нажимаем Enter, чтобы пропустить его.
   - Через OpenCV ищем на экране шаблон `benchmark-button.png` и кликаем по нему.
   - Ищем `confirm-benchmark.png` и подтверждаем запуск.
   - Ждём появления `benchmark-results.png` (таймаут — 20 минут).

5. **Ожидание логов.**
   После появления экрана результатов оркестратор проверяет папку истории каждые 1 секунду. Как только появляются новые или изменённые файлы — ждёт 3 секунды их стабилизации (чтобы файлы дописались) и копирует их в `BenchmarkLogs/<TEST>_<timestamp>/`.

6. **Закрытие бенчмарка.**
   Процесс закрывается через `CloseMainWindow()`, при необходимости — через `Kill()`. Дополнительно убиваются все процессы с именами `b1_benchmark` и `b1-Win64-Shipping`.

7. **Повтор для второго теста.**
   Всё то же самое выполняется для второго профиля (CPU → GPU).

8. **Формирование отчёта.**
   `BenchmarkLogParser` читает самые свежие JSON-логи из `BenchmarkLogs/CPU_*` и `BenchmarkLogs/GPU_*`, извлекает метрики FPS, характеристики ПК и настройки, а затем формирует единый текстовый отчёт и сохраняет его в `Reports/BenchmarkReport.txt`.

---

## Настройки для тестов

### CPU-тест

Цель — снять нагрузку с GPU и оставить нагрузку на CPU.

```
Разрешение:         1280×720
QualityLevel:       1
Dlss:               0
InsertFrame:        0
Rtx:                0
RtxLevel:           0
SuperResolution:    0
AntiAliasing:       0
ShadowQuality:      0
PostProcessing:     0
TextureQuality:     0
GlobalIllumination: 0
ReflectionQuality:  0
ViewDistance:       0
FxQuality:          0
MaterialQuality:    0
VegetationQuality:  0
ImageQuality:       720
```

### GPU-тест

Цель — максимально нагрузить видеокарту.

```
Разрешение:         1920 × 1080
QualityLevel:       6
Dlss:               1
InsertFrame:        1
Rtx:                1
RtxLevel:           1
SuperResolution:    1
AntiAliasing:       3
ShadowQuality:      3
PostProcessing:     3
TextureQuality:     3
GlobalIllumination: 2
ReflectionQuality:  3
ViewDistance:       3
FxQuality:          3
MaterialQuality:    3
VegetationQuality:  3
ImageQuality:       1440
```

---

## Запуск

### Обычный запуск (полный цикл)

```bash
dotnet run
```

Инструмент применит CPU-конфигурацию, прогонит бенчмарк, скопирует логи, затем проделает то же самое для GPU-конфигурации и сформирует отчёт.

### Только отчёт (без запуска тестов)

Если прогоны уже были сделаны ранее, а нужно только пересобрать отчёт:

```bash
dotnet run -- --report
```

В этом режиме оркестратор не запускается, а `BenchmarkLogParser` просто читает имеющиеся логи в `BenchmarkLogs/`.

---

## Формат отчёта

Отчёт выводится в консоль и сохраняется в `Reports/BenchmarkReport.txt`. Пример:

```
WUKONG BENCHMARK REPORT
============================================================

[Характеристики ПК]:
CPU: AMD Ryzen 5 5600H with Radeon Graphics
GPU: NVIDIA GeForce RTX 3050 Ti Laptop GPU
RAM: 14GB
VRAM: 4GB

[CPU]:
----------------------------
AVG FPS: 98
MIN FPS: 66
MAX FPS: 114
total VRAM used: 3.60 GB
FPS-95: 87

[Настройки использованные для CPU-теста]:
- Разрешение: 1280 × 720
- QualityLevel: 1
- DLSS: 0
- RTX: 0
- ShadowQuality: 0
- TextureQuality: 0
- ViewDistance: 0
- VegetationQuality: 0

[GPU]:
----------------------------
AVG FPS: 65
MIN FPS: 42
MAX FPS: 88
total VRAM used: 3.85 GB
FPS-95: 58

[Настройки использованные для GPU-теста]:
- Разрешение: 1920 × 1080
- QualityLevel: 6
- DLSS: 1
- RTX: 1
- ShadowQuality: 3
- TextureQuality: 3
- ViewDistance: 3
- VegetationQuality: 3

============================================================
```

---

## Конфигурация путей

В проекте есть несколько путей, которые жёстко прописаны. При переносе проекта на другую машину их нужно изменить.

### Путь к `GameUserSettings.ini`

В `ConfigManager.cs`:

```csharp
private const string ConfigPath =
    @"C:\Program Files (x86)\Steam\steamapps\common\Black Myth Wukong Benchmark Tool\b1\Saved\Config\Windows\GameUserSettings.ini";
```

Если Steam установлен в другую папку — поправьте путь.

### Путь к папке логов бенчмарка

В `BenchmarkOrchestrator.cs`:

```csharp
_logDirectory = logDirectory ??
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Temp", "b1", "BenchMarkHistory", "Tool");
```

Обычно это `%LOCALAPPDATA%\Temp\b1\BenchMarkHistory\Tool`. Путь можно передать в конструктор.

### Путь к проекту (для выходных папок)

В `Program.cs` и `BenchmarkOrchestrator.cs` есть жёстко прописанный путь:

```csharp
string projectDirectory =
    @"C:\Users\User\source\repos\test_progon_benchmark\test_progon_benchmark";
```

Он используется для создания `BenchmarkLogs` и `Reports`. При переносе проекта замените его на актуальный или перепишите на `AppContext.BaseDirectory`.
