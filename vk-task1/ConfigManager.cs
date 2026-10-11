using System.Text.RegularExpressions;

namespace WukongBench;

public static class ConfigManager
{
    private const string ConfigPath =
        @"C:\Program Files (x86)\Steam\steamapps\common\Black Myth Wukong Benchmark Tool\b1\Saved\Config\Windows\GameUserSettings.ini";

    public static void SetSettingsForCPU()
    {
        Console.WriteLine("Применение настроек для CPU-теста...");

        var settings = new BenchmarkConfig
        {
            DesiredScreenWidth = 1280,
            DesiredScreenHeight = 720,
            ResolutionSizeX = 1280,
            ResolutionSizeY = 720,
            FullscreenMode = 1,

            ImageQuality = 720,
            QualityLevel = 1,
            Dlss = 0,
            InsertFrame = 0,
            Rtx = 0,
            RtxLevel = 0,

            ViewDistance = 0,
            AntiAliasing = 0,
            PostProcessing = 0,
            ShadowQuality = 0,
            TextureQuality = 0,
            FxQuality = 0,
            MaterialQuality = 0,
            VegetationQuality = 0,
            GlobalIllumination = 0,
            ReflectionQuality = 0,
            SuperResolutionSampling = 0
        };

        UpdateConfigFile(settings);
    }

    public static void SetSettingsForGPU()
    {
        Console.WriteLine("Применение настроек для GPU-теста...");

        var settings = new BenchmarkConfig
        {
            DesiredScreenWidth = 2560,
            DesiredScreenHeight = 1440,
            ResolutionSizeX = 2560,
            ResolutionSizeY = 1440,
            FullscreenMode = 1,

            ImageQuality = 1440,
            QualityLevel = 6,
            Dlss = 1,
            InsertFrame = 1,
            Rtx = 1,
            RtxLevel = 1,

            ViewDistance = 3,
            AntiAliasing = 3,
            PostProcessing = 3,
            ShadowQuality = 3,
            TextureQuality = 3,
            FxQuality = 3,
            MaterialQuality = 3,
            VegetationQuality = 3,
            GlobalIllumination = 2,
            ReflectionQuality = 3,
            SuperResolutionSampling = 1
        };

        UpdateConfigFile(settings);
    }

    private static void UpdateConfigFile(BenchmarkConfig settings)
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException("Не найден файл конфигурации Benchmark Tool.", ConfigPath);
        }

        try
        {
            string content = File.ReadAllText(ConfigPath);

            // Сохраняем резервную копию исходного конфига.
            string backupPath = ConfigPath + ".backup";

            if (!File.Exists(backupPath))
            {
                File.Copy(ConfigPath, backupPath);
                Console.WriteLine($"  → Создана резервная копия: {backupPath}");
            }

            // Основные параметры разрешения.
            content = UpdateSimpleKey(
                content, "DesiredScreenWidth",
                settings.DesiredScreenWidth);

            content = UpdateSimpleKey(
                content, "DesiredScreenHeight",
                settings.DesiredScreenHeight);

            content = UpdateSimpleKey(
                content, "ResolutionSizeX",
                settings.ResolutionSizeX);

            content = UpdateSimpleKey(
                content, "ResolutionSizeY",
                settings.ResolutionSizeY);

            content = UpdateSimpleKey(
                content, "LastUserConfirmedResolutionSizeX",
                settings.ResolutionSizeX);

            content = UpdateSimpleKey(
                content, "LastUserConfirmedResolutionSizeY",
                settings.ResolutionSizeY);

            content = UpdateSimpleKey(
                content, "LastUserConfirmedDesiredScreenWidth",
                settings.DesiredScreenWidth);

            content = UpdateSimpleKey(
                content, "LastUserConfirmedDesiredScreenHeight",
                settings.DesiredScreenHeight);

            content = UpdateSimpleKey(
                content, "FullscreenMode",
                settings.FullscreenMode);

            content = UpdateSimpleKey(
                content, "LastConfirmedFullscreenMode",
                settings.FullscreenMode);

            content = UpdateSimpleKey(
                content, "PreferredFullscreenMode",
                settings.FullscreenMode);


            content = UpdateUISettingData(content, settings);


            // Настройки групп качества Unreal Engine.
            content = UpdateSimpleKey(
                content, "sg.ViewDistanceQuality",
                settings.ViewDistance);

            content = UpdateSimpleKey(
                content, "sg.AntiAliasingQuality",
                settings.AntiAliasing);

            content = UpdateSimpleKey(
                content, "sg.ShadowQuality",
                settings.ShadowQuality);

            content = UpdateSimpleKey(
                content, "sg.GlobalIlluminationQuality",
                settings.GlobalIllumination);

            content = UpdateSimpleKey(
                content, "sg.RayTracingQuality",
                settings.Rtx == 0 ? 0 : settings.RtxLevel);

            content = UpdateSimpleKey(
                content, "sg.ReflectionQuality",
                settings.ReflectionQuality);

            content = UpdateSimpleKey(
                content, "sg.PostProcessQuality",
                settings.PostProcessing);

            content = UpdateSimpleKey(
                content, "sg.TextureQuality",
                settings.TextureQuality);

            content = UpdateSimpleKey(
                content, "sg.EffectsQuality",
                settings.FxQuality);

            content = UpdateSimpleKey(
                content, "sg.FoliageQuality",
                settings.VegetationQuality);

            content = UpdateSimpleKey(
                content, "sg.ShadingQuality",
                settings.MaterialQuality);

            File.WriteAllText(ConfigPath, content);

            Console.WriteLine("[Конфигурация успешно обновлена.]");
            Console.WriteLine(
                $"[Разрешение: {settings.ResolutionSizeX}x{settings.ResolutionSizeY}]");
            Console.WriteLine(
                $"[QualityLevel: {settings.QualityLevel}]");
            Console.WriteLine(
                $"[DLSS: {settings.Dlss}, Frame Generation: {settings.InsertFrame}, RTX: {settings.Rtx}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ОШИБКА при изменении конфига: {ex}");

            throw;
        }
    }

    private static string UpdateSimpleKey(
        string content,
        string key,
        int newValue)
    {
        string pattern = $@"(?m)^({Regex.Escape(key)}=).*$";

        if (!Regex.IsMatch(content, pattern))
        {
            Console.WriteLine($"ВНИМАНИЕ: ключ {key} не найден.");
            return content;
        }

        return Regex.Replace(content, pattern, _ => $"{key}={newValue}");
    }

    private static string UpdateUISettingData(string content, BenchmarkConfig settings)
    {
        var lineMatch = Regex.Match(content, @"(?m)^UISettingData=.*$");

        if (!lineMatch.Success)
        {
            throw new InvalidDataException("В конфиге отсутствует строка UISettingData.");
        }

        string originalLine = lineMatch.Value;

        var replacements = new Dictionary<string, string>
        {
            ["ImageQuality"] = settings.ImageQuality.ToString(),
            ["ScreenMode"] = settings.FullscreenMode.ToString(),
            ["Dlss"] = settings.Dlss.ToString(),
            ["InsertFrame"] = settings.InsertFrame.ToString(),
            ["Rtx"] = settings.Rtx.ToString(),
            ["RtxLevel"] = settings.RtxLevel.ToString(),
            ["QualityLevel"] = settings.QualityLevel.ToString(),
            ["ViewDistance"] = settings.ViewDistance.ToString(),
            ["AntiAliasing"] = settings.AntiAliasing.ToString(),
            ["PostProcessing"] = settings.PostProcessing.ToString(),
            ["ShadowQuality"] = settings.ShadowQuality.ToString(),
            ["TextureQuality"] = settings.TextureQuality.ToString(),
            ["FxQuality"] = settings.FxQuality.ToString(),
            ["MaterialQuality"] = settings.MaterialQuality.ToString(),
            ["VegetationQuality"] = settings.VegetationQuality.ToString(),
            ["GlobalIllumination"] = settings.GlobalIllumination.ToString(),
            ["ReflectionQuality"] = settings.ReflectionQuality.ToString(),
            ["SuperResolutionSampling"] = settings.SuperResolutionSampling.ToString()
        };

        var foundKeys = new HashSet<string>();

        string updatedLine = Regex.Replace(
            originalLine,
            @"\(""(?<key>[^""]+)"",\s*""(?<value>[^""]*)""\)",
            match =>
            {
                string key = match.Groups["key"].Value;

                if (!replacements.TryGetValue(key, out string? value))
                {
                    return match.Value;
                }

                foundKeys.Add(key);

                return $"(\"{key}\", \"{value}\")";
            });

        foreach (string key in replacements.Keys)
        {
            if (!foundKeys.Contains(key))
            {
                Console.WriteLine($"ВНИМАНИЕ: параметр {key} отсутствует в UISettingData.");
            }
        }

        return content.Replace(originalLine, updatedLine);
    }

    private sealed class BenchmarkConfig
    {
        public int DesiredScreenWidth { get; set; }
        public int DesiredScreenHeight { get; set; }
        public int ResolutionSizeX { get; set; }
        public int ResolutionSizeY { get; set; }
        public int FullscreenMode { get; set; }
        public int ImageQuality { get; set; }

        public int QualityLevel { get; set; }
        public int Dlss { get; set; }
        public int InsertFrame { get; set; }
        public int Rtx { get; set; }
        public int RtxLevel { get; set; }
        public int ViewDistance { get; set; }
        public int AntiAliasing { get; set; }
        public int PostProcessing { get; set; }
        public int ShadowQuality { get; set; }
        public int TextureQuality { get; set; }
        public int FxQuality { get; set; }
        public int MaterialQuality { get; set; }
        public int VegetationQuality { get; set; }
        public int GlobalIllumination { get; set; }
        public int ReflectionQuality { get; set; }
        public int SuperResolutionSampling { get; set; }
    }
}
