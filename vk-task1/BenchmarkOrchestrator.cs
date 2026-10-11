using System.Diagnostics;

namespace WukongBench
{
    public sealed class BenchmarkOrchestrator
    {
        private const string SteamUri = "steam://rungameid/3132990";

        private const string BenchmarkTemplate = "benchmark-button.png";
        private const string ConfirmTemplate = "confirm-benchmark.png";
        private const string ResultsTemplate = "benchmark-results.png";

        private static readonly TimeSpan WindowTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ResultsTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan LogTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan LogStableDuration = TimeSpan.FromSeconds(3);

        private readonly string _logDirectory;
        private readonly string _outputDirectory;

        public BenchmarkOrchestrator(string? logDirectory = null, string? outputDirectory = null)
        {
            _logDirectory = logDirectory ??
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Temp", "b1", "BenchMarkHistory", "Tool");

            string projectDirectory =
                @"C:\Users\DNS\source\repos\test_progon_benchmark\test_progon_benchmark";

            _outputDirectory = outputDirectory ??
                Path.Combine(projectDirectory, "BenchmarkLogs");
        }

        public void RunAll(Action applyCpuConfig, Action applyGpuConfig)
        {
            Directory.CreateDirectory(_outputDirectory);

            ScreenAutomation.MoveCursor(1, 1);

            RunOne("CPU", applyCpuConfig);

            ScreenAutomation.MoveCursor(1, 1);

            RunOne("GPU", applyGpuConfig);

            Console.WriteLine();
            Console.WriteLine("Все прогоны завершены.");
            Console.WriteLine($"Логи сохранены в: {_outputDirectory}");
        }

        public void RunOne(string name, Action applyConfiguration)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {name} BENCHMARK ===");

            Process? benchmarkProcess = null;

            try
            {
                Console.WriteLine($"Применение конфигурации под {name}");
                applyConfiguration();

                Directory.CreateDirectory(_logDirectory);

                Dictionary<string, FileInfoSnapshot> before = GetLogSnapshot();

                Console.WriteLine($"В исходном снимке файлов: {before.Count}");

                StartBenchmarkTool();

                benchmarkProcess = WaitForBenchmarkProcess(WindowTimeout);

                Console.WriteLine("Ожидание приветственного экрана");
                Console.WriteLine("Окно открыто. Ждём 40 секунд");
                Thread.Sleep(TimeSpan.FromSeconds(40));

                ScreenAutomation.PressEnter();

                ScreenAutomation.WaitForImageAndClick(
                    BenchmarkTemplate,
                    "Кнопка Benchmark",
                    TimeSpan.FromMinutes(2));

                ScreenAutomation.WaitForImageAndPressEnter(
                    ConfirmTemplate,
                    "Подтверждение запуска бенчмарка",
                    TimeSpan.FromMinutes(1));

                Console.WriteLine($"Ожидание результатов Benchmark для {name}");

                ScreenAutomation.WaitForImage(
                    ResultsTemplate,
                    "Экран Benchmark results",
                    ResultsTimeout);

                Console.WriteLine("Экран результатов найден. Проверка логов.");

                List<string> changedLogs = WaitForChangedLogs(before, LogTimeout, LogStableDuration);

                if (changedLogs.Count == 0)
                {
                    throw new IOException(
                        "Экран результатов появился, но новых или изменённых файлов логов не обнаружено.");
                }

                SaveRunLogs(name, changedLogs);

                Console.WriteLine($"Прогон конфигурации под {name} завершён.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА при прогоне под {name}: {ex.Message}");
                throw;
            }
            finally
            {
                CloseBenchmarkTool(benchmarkProcess);
            }
        }

        private static void StartBenchmarkTool()
        {
            Console.WriteLine("Запускаем Benchmark Tool");

            var startInfo = new ProcessStartInfo
            {
                FileName = SteamUri,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }

        private static Process WaitForBenchmarkProcess(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            string[] processNames =
            {
                "b1_benchmark",
                "b1-Win64-Shipping",
                "b1"
            };

            while (DateTime.UtcNow < deadline)
            {
                foreach (string processName in processNames)
                {
                    Process[] processes;

                    try
                    {
                        processes = Process.GetProcessesByName(processName);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Process process in processes)
                    {
                        try
                        {
                            process.Refresh();

                            if (!process.HasExited &&
                                process.MainWindowHandle != IntPtr.Zero)
                            {
                                Console.WriteLine($"Найден процесс: {process.ProcessName}");
                                return process;
                            }
                        }
                        catch
                        {
                            process.Dispose();
                        }
                    }
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException(
                "Не удалось найти окно Benchmark Tool. Проверьте имя процесса и запуск через Steam.");
        }

        private static void CloseBenchmarkTool(Process? process)
        {
            Console.WriteLine("Закрытие Benchmark Tool...");

            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();

                        if (!process.WaitForExit(5000))
                            process.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при закрытии процесса: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            foreach (string name in new[]
                     {
                         "b1_benchmark",
                         "b1-Win64-Shipping"
                     })
            {
                foreach (Process extra in
                         Process.GetProcessesByName(name))
                {
                    try
                    {
                        extra.Kill(true);
                    }
                    catch { }
                    finally
                    {
                        extra.Dispose();
                    }
                }
            }

            Thread.Sleep(1500);
        }

        private Dictionary<string, FileInfoSnapshot> GetLogSnapshot()
        {
            var snapshot =
                new Dictionary<string, FileInfoSnapshot>(
                    StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(_logDirectory))
                return snapshot;

            foreach (string file in Directory.GetFiles(
                         _logDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);

                    snapshot[file] = new FileInfoSnapshot(
                        info.Length,
                        info.LastWriteTimeUtc);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            return snapshot;
        }

        private List<string> WaitForChangedLogs(
            Dictionary<string, FileInfoSnapshot> before,
            TimeSpan timeout,
            TimeSpan stableDuration)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            Dictionary<string, FileInfoSnapshot> previous = GetLogSnapshot();

            bool changed = HasDifferences(before, previous);
            DateTime? lastChange = changed ? DateTime.UtcNow : null;

            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(1000);

                Dictionary<string, FileInfoSnapshot> current = GetLogSnapshot();

                if (HasDifferences(previous, current))
                {
                    previous = current;
                    changed = changed || HasDifferences(before, current);
                    lastChange = DateTime.UtcNow;

                    Console.WriteLine("Обнаружено изменение логов.");
                }

                if (changed &&
                    lastChange.HasValue &&
                    DateTime.UtcNow - lastChange.Value >= stableDuration)
                {
                    return FindChangedFiles(before, current);
                }
            }

            throw new TimeoutException(
                "После появления экрана результатов логи не стабилизировались");
        }

        private static bool HasDifferences(
            Dictionary<string, FileInfoSnapshot> first,
            Dictionary<string, FileInfoSnapshot> second)
        {
            if (first.Count != second.Count)
                return true;

            foreach (var item in second)
            {
                if (!first.TryGetValue(item.Key, out var old))
                    return true;

                if (old.Length != item.Value.Length ||
                    old.LastWriteUtc != item.Value.LastWriteUtc)
                    return true;
            }

            return false;
        }

        private static List<string> FindChangedFiles(
            Dictionary<string, FileInfoSnapshot> before,
            Dictionary<string, FileInfoSnapshot> after)
        {
            var files = new List<string>();

            foreach (var item in after)
            {
                if (!before.TryGetValue(item.Key, out var old) ||
                    old.Length != item.Value.Length ||
                    old.LastWriteUtc != item.Value.LastWriteUtc)
                {
                    files.Add(item.Key);
                }
            }

            return files;
        }

        private void SaveRunLogs(string runName, List<string> files)
        {
            string runDirectory = Path.Combine(
                _outputDirectory,
                $"{runName}_{DateTime.Now:yyyyMMdd_HHmmss}");

            Directory.CreateDirectory(runDirectory);

            foreach (string source in files)
            {
                try
                {
                    string relative = Path.GetRelativePath(
                        _logDirectory, source);

                    string destination = Path.Combine(
                        runDirectory, relative);

                    string? parent = Path.GetDirectoryName(destination);

                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    File.Copy(source, destination, true);

                    Console.WriteLine($"Сохранён лог: {relative}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Не удалось скопировать {source}: {ex.Message}");
                }
            }

            Console.WriteLine($"→ Копии логов: {runDirectory}");
        }

        private sealed class FileInfoSnapshot
        {
            public long Length { get; }
            public DateTime LastWriteUtc { get; }

            public FileInfoSnapshot(long length, DateTime lastWriteUtc)
            {
                Length = length;
                LastWriteUtc = lastWriteUtc;
            }
        }
    }
}
