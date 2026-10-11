using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WukongBench;

public static class BenchmarkLogParser
{
    public static string CreateReport(string logsDirectory)
    {
        var report = new StringBuilder();

        report.AppendLine("WUKONG BENCHMARK REPORT");
        report.AppendLine(new string('=', 60));
        report.AppendLine();

        using JsonDocument? cpu = LoadLatestJson(logsDirectory, "CPU");
        using JsonDocument? gpu = LoadLatestJson(logsDirectory, "GPU");
        JsonElement? system = cpu?.RootElement ?? gpu?.RootElement;

        report.AppendLine("[Характеристики ПК]:");

        if (system.HasValue)
        {
            JsonElement root = system.Value;

            report.AppendLine($"CPU: {GetString(root, "CPUModel")}");
            report.AppendLine($"GPU: {GetString(root, "GPUModel")}");
            report.AppendLine($"RAM: {GetString(root, "SysMem")}");
            report.AppendLine($"VRAM: {GetString(root, "VideoMemSize")}");
        }

        report.AppendLine();

        AppendTest(report, "CPU", cpu);
        AppendTest(report, "GPU", gpu);

        report.AppendLine(new string('=', 60));

        return report.ToString();
    }

    private static JsonDocument? LoadLatestJson(string logsDirectory,string testName)
    {
        if (!Directory.Exists(logsDirectory))
        {
            Console.WriteLine("Папка логов не найдена: " + logsDirectory);

            return null;
        }

        var files = Directory
            .GetFiles(logsDirectory,"*",SearchOption.AllDirectories)
            .Where(file => BelongsToTest(file, testName))
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (string file in files)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var candidate = JsonDocument.Parse(stream);

                JsonElement root = candidate.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    continue;

                if (!root.TryGetProperty("FPSAvg", out _) ||
                    !root.TryGetProperty("FPSMin", out _) ||
                    !root.TryGetProperty("FPSMax", out _))
                {
                    continue;
                }

                Console.WriteLine(
                    $"Найден JSON для {testName}: {file}");

                return JsonDocument.Parse(root.GetRawText());
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Не удалось прочитать " + $"{file}: {ex.Message}");
            }
        }

        Console.WriteLine($"Результаты {testName} не найдены.");

        return null;
    }

    private static bool BelongsToTest(string filePath, string testName)
    {
        string? directory = Path.GetDirectoryName(filePath);

        while (!string.IsNullOrEmpty(directory))
        {
            string folderName = Path.GetFileName(directory);

            if (folderName.StartsWith(testName + "_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static void AppendTest(StringBuilder report, string testName, JsonDocument? document)
    {
        report.AppendLine($"[{testName}]:");
        report.AppendLine(new string('-', 28));

        if (document is null)
        {
            report.AppendLine("Результаты теста не найдены.");
            report.AppendLine();
            return;
        }

        JsonElement root = document.RootElement;

        report.AppendLine($"AVG FPS: {GetValue(root, "FPSAvg")}");
        report.AppendLine($"MIN FPS: {GetValue(root, "FPSMin")}");
        report.AppendLine($"MAX FPS: {GetValue(root, "FPSMax")}");
        report.AppendLine($"total VRAM used: {FormatVram(root)}");
        report.AppendLine($"FPS-95: {GetValue(root, "FPS95")}");

        report.AppendLine();
        report.AppendLine(
            $"[Настройки использованные для {testName}-теста]:");
        report.AppendLine(
            $"- Разрешение: {GetString(root, "ScreenResolution")}");
        report.AppendLine(
            $"- QualityLevel: {GetValue(root, "QualityLevel")}");
        report.AppendLine(
            $"- DLSS: {GetValue(root, "Dlss")}");
        report.AppendLine(
            $"- RTX: {GetValue(root, "Rtx")}");
        report.AppendLine(
            $"- ShadowQuality: {GetValue(root, "ShadowQuality")}");
        report.AppendLine(
            $"- TextureQuality: {GetValue(root, "TextureQuality")}");
        report.AppendLine(
            $"- ViewDistance: {GetValue(root, "ViewDistance")}");
        report.AppendLine(
            $"- VegetationQuality: {GetValue(root, "VegetationQuality")}");
        report.AppendLine();
    }

    private static string GetString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement value))
            return "Не найдено";

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "Не найдено"
            : value.ToString();
    }

    private static string GetValue(
        JsonElement root,
        string property)
    {
        return root.TryGetProperty(property, out JsonElement value)
            ? value.ToString()
            : "Не найдено";
    }

    private static string FormatVram(JsonElement root)
    {
        if (!root.TryGetProperty("VideoMem", out JsonElement value) ||
            !value.TryGetDouble(out double vram))
        {
            return "Не найдено";
        }

        return $"{vram.ToString("F2", CultureInfo.InvariantCulture)} GB";
    }
}
