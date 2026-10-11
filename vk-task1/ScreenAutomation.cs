using System.Runtime.InteropServices;
using OpenCvSharp;

namespace WukongBench
{
    public static class ScreenAutomation
    {
        private const int DIB_RGB_COLORS = 0;
        private const uint SRCCOPY = 0x00CC0020;
        private const uint BI_RGB = 0;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private static readonly string TemplateDirectory = Path.Combine(AppContext.BaseDirectory, "Templates");

        public static double MatchThreshold { get; set; } = 0.95;

        public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(2);
        public static TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        static ScreenAutomation()
        {
            SetProcessDPIAware();
        }

        public static void MoveCursor(int x, int y)
        {
            if (!SetCursorPos(x, y)) throw new InvalidOperationException("Не удалось переместить курсор.");
        }

        public static string TemplatePath(string fileName)
        {
            string path = Path.Combine(TemplateDirectory, fileName);

            if (!File.Exists(path)) throw new FileNotFoundException($"Не найден шаблон интерфейса: {path}", path);

            return path;
        }

        public static void WaitForImageAndPressEnter(string templateFile, string description, TimeSpan? timeout = null)
        {
            WaitForImage(templateFile, description, timeout);

            Console.WriteLine($"Нажимаем Enter: {description}");
            PressEnter();
        }

        public static void WaitForImageAndClick(string templateFile, string description, TimeSpan? timeout = null)
        {
            OpenCvSharp.Point center = WaitForImage(templateFile, description, timeout);

            if (!SetCursorPos(center.X, center.Y))
                throw new InvalidOperationException(
                    "Не удалось переместить курсор.");

            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(100);

            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(500);
        }

        public static OpenCvSharp.Point WaitForImage(string templateFile, string description, TimeSpan? timeout = null)
        {
            string path = TemplatePath(templateFile);
            TimeSpan limit = timeout ?? DefaultTimeout;
            DateTime deadline = DateTime.UtcNow + limit;

            using Mat template = Cv2.ImRead(path, ImreadModes.Color);

            if (template.Empty())
                throw new InvalidOperationException(
                    $"Не удалось загрузить изображение: {path}");

            using Mat templateGray = new Mat();
            Cv2.CvtColor(
                template, templateGray, ColorConversionCodes.BGR2GRAY);

            Console.WriteLine($"Ожидаем: {description}");

            while (DateTime.UtcNow < deadline)
            {
                using Mat screen = CaptureScreen();
                using Mat screenGray = new Mat();

                Cv2.CvtColor(
                    screen, screenGray, ColorConversionCodes.BGR2GRAY);

                if (screenGray.Width >= templateGray.Width &&
                    screenGray.Height >= templateGray.Height)
                {
                    using Mat result = new Mat();

                    Cv2.MatchTemplate(
                        screenGray,
                        templateGray,
                        result,
                        TemplateMatchModes.CCoeffNormed);

                    Cv2.MinMaxLoc(
                        result,
                        out _,
                        out double maxValue,
                        out _,
                        out OpenCvSharp.Point maxLocation);


                    if (maxValue >= MatchThreshold)
                    {
                        int centerX =
                            maxLocation.X + templateGray.Width / 2;
                        int centerY =
                            maxLocation.Y + templateGray.Height / 2;

                        return new OpenCvSharp.Point(centerX, centerY);
                    }
                }

                Thread.Sleep(PollInterval);
            }

            throw new TimeoutException($"Не найден элемент интерфейса «{description}» ");
        }

        public static Mat CaptureScreen()
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

            int width = bounds.Width;
            int height = bounds.Height;

            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Не удалось получить DC экрана.");

            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldObject = IntPtr.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Не удалось создать совместимый DC.");

                bitmap = CreateCompatibleBitmap(screenDc, width, height);
                if (bitmap == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Не удалось создать bitmap экрана.");

                oldObject = SelectObject(memoryDc, bitmap);
                if (oldObject == IntPtr.Zero ||
                    oldObject == new IntPtr(-1))
                    throw new InvalidOperationException(
                        "Не удалось выбрать bitmap.");

                if (!BitBlt(
                        memoryDc, 0, 0, width, height,
                        screenDc, bounds.Left, bounds.Top, SRCCOPY))
                {
                    throw new InvalidOperationException(
                        "Не удалось скопировать изображение экрана.");
                }

                // GetDIBits вызывается после отсоединения bitmap от DC.
                SelectObject(memoryDc, oldObject);
                oldObject = IntPtr.Zero;

                var info = new BITMAPINFO();
                info.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                info.bmiHeader.biWidth = width;
                info.bmiHeader.biHeight = -height;
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = BI_RGB;
                info.bmiHeader.biSizeImage = (uint)((long)width * height * 4);

                using Mat bgra = new Mat(height, width, MatType.CV_8UC4);

                int lines = GetDIBits(
                    screenDc,
                    bitmap,
                    0,
                    (uint)height,
                    bgra.Data,
                    ref info,
                    DIB_RGB_COLORS);

                if (lines == 0)
                    throw new InvalidOperationException(
                        "Не удалось получить пиксели экрана через GetDIBits.");

                Mat bgr = new Mat();

                try
                {
                    Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
                    return bgr;
                }
                catch
                {
                    bgr.Dispose();
                    throw;
                }
            }
            finally
            {
                if (oldObject != IntPtr.Zero &&
                    oldObject != new IntPtr(-1) &&
                    memoryDc != IntPtr.Zero)
                {
                    SelectObject(memoryDc, oldObject);
                }

                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);

                if (memoryDc != IntPtr.Zero)
                    DeleteDC(memoryDc);

                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public static void PressEnter()
        {
            keybd_event(0x0D, 0, 0, UIntPtr.Zero);
            Thread.Sleep(100);
            keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(500);
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr hdcDest,
            int xDest, int yDest,
            int width, int height,
            IntPtr hdcSource,
            int xSource, int ySource,
            uint rasterOperation);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hBitmap,
            uint start,
            uint lines,
            IntPtr bits,
            ref BITMAPINFO info,
            uint usage);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(
            uint flags, uint dx, uint dy,
            uint data, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(
            byte virtualKey, byte scanCode,
            uint flags, UIntPtr extraInfo);
    }
}
