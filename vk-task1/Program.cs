using System.Text;

namespace WukongBench;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("--- Автоматизированный запуск Wukong Benchmark Tool ---");

        string projectDirectory = @"C:\Users\DNS\source\repos\test_progon_benchmark\test_progon_benchmark";
        string logsDirectory = Path.Combine(projectDirectory, "BenchmarkLogs");
        string reportsDirectory = Path.Combine(projectDirectory, "Reports");

        try
        {
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(reportsDirectory);

            bool reportOnly = Array.Exists(
                args,
                arg => string.Equals(
                    arg, "--report",
                    StringComparison.OrdinalIgnoreCase));

            if (!reportOnly)
            {
                var orchestrator = new BenchmarkOrchestrator(outputDirectory: logsDirectory);

                orchestrator.RunAll(
                    applyCpuConfig: () => ConfigManager.SetSettingsForCPU(),
                    applyGpuConfig: () => ConfigManager.SetSettingsForGPU());

                Console.WriteLine();
                Console.WriteLine("Оба прогона завершены.");
            }
            else
            {
                Console.WriteLine(
                    "Режим создания отчёта без запуска тестов.");
            }

            string report = BenchmarkLogParser.CreateReport(logsDirectory);

            string reportPath = Path.Combine(reportsDirectory, "BenchmarkReport.txt");

            File.WriteAllText(reportPath, report, new UTF8Encoding(false));

            Console.WriteLine();
            Console.WriteLine(report);
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Логи прогонов: {logsDirectory}");
            Console.WriteLine($"Итоговый отчёт: {reportPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("ОШИБКА");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
