using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SkiaSharpScreenCapture
{
    public partial class MainWindow : Window
    {
        private SKImage capturedImage;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnCaptureClicked(object sender, RoutedEventArgs e)
        {
            // get the screen scaling
            var source = PresentationSource.FromVisual(this);
            var m = source.CompositionTarget.TransformToDevice;

            Hide();

            // capture the image
            capturedImage = CaptureRegion(
                (int)SystemParameters.VirtualScreenLeft,
                (int)SystemParameters.VirtualScreenTop,
                (int)(SystemParameters.VirtualScreenWidth * m.M11),
                (int)(SystemParameters.VirtualScreenHeight * m.M22));

            Show();

            // create the new painting surface
            var preview = new SKElement();
            preview.PaintSurface += OnPaintSurface;
            preview.HorizontalAlignment = HorizontalAlignment.Stretch;
            preview.VerticalAlignment = VerticalAlignment.Stretch;

            // create a popup
            var window = new Window
            {
                Content = preview,
            };
            window.ShowDialog();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (capturedImage == null)
                return;

            var info = e.Info;
            var canvas = e.Surface.Canvas;

            var imageSize = new SKSizeI(capturedImage.Width, capturedImage.Height);
            var fitRect = info.Rect.AspectFit(imageSize);

            canvas.DrawImage(capturedImage, SKRect.Create(imageSize), fitRect);
        }

        public static SKImage CaptureRegion(int x, int y, int width, int height)
        {
            IntPtr sourceDC = IntPtr.Zero;
            IntPtr targetDC = IntPtr.Zero;
            IntPtr compatibleBitmapHandle = IntPtr.Zero;

            // create the final SkiaSharp image
            SKImage image = SKImage.Create(new SKImageInfo(width, height));
            SKPixmap pixmap = image.PeekPixels();

            try
            {
                // gets the main desktop and all open windows
                sourceDC = User32.GetDC(User32.GetDesktopWindow());
                targetDC = Gdi32.CreateCompatibleDC(sourceDC);

                // create a bitmap compatible with our target DC
                compatibleBitmapHandle = Gdi32.CreateCompatibleBitmap(sourceDC, width, height);

                // gets the bitmap into the target device context
                Gdi32.SelectObject(targetDC, compatibleBitmapHandle);

                // copy from source to destination
                Gdi32.BitBlt(targetDC, 0, 0, width, height, sourceDC, x, y, Gdi32.TernaryRasterOperations.SRCCOPY);

                // create the info structure
                Gdi32.BITMAPINFOHEADER bmi = new Gdi32.BITMAPINFOHEADER();
                bmi.biPlanes = 1;
                bmi.biBitCount = 32;
                bmi.biWidth = width;
                bmi.biHeight = -height;
                bmi.biCompression = Gdi32.BitmapCompressionMode.BI_RGB;

                // read the raw pixels into the pixmap for the image
                Gdi32.GetDIBits(targetDC, compatibleBitmapHandle, 0, height, pixmap.GetPixels(), bmi, Gdi32.DIB_Color_Mode.DIB_RGB_COLORS);
            }
            finally
            {
                Gdi32.DeleteObject(compatibleBitmapHandle);

                User32.ReleaseDC(IntPtr.Zero, sourceDC);
                User32.ReleaseDC(IntPtr.Zero, targetDC);
            }

            return image;
        }

        static class User32
        {
            const string DllName = "user32.dll";

            [DllImport(DllName, SetLastError = false)]
            public static extern IntPtr GetDesktopWindow();

            [DllImport(DllName)]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport(DllName)]
            public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);
        }

        static class Gdi32
        {
            const string DllName = "gdi32.dll";

            [DllImport(DllName, SetLastError = true)]
            public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

            [DllImport(DllName)]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

            [DllImport(DllName)]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

            [DllImport(DllName, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

            [DllImport(DllName)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteObject(IntPtr hObject);

            [DllImport(DllName)]
            public static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, int uStartScan, int cScanLines, IntPtr lpvBits, BITMAPINFOHEADER lpbi, DIB_Color_Mode uUsage);

            public enum TernaryRasterOperations : uint
            {
                SRCCOPY = 0x00CC0020,
                SRCPAINT = 0x00EE0086,
                SRCAND = 0x008800C6,
                SRCINVERT = 0x00660046,
                SRCERASE = 0x00440328,
                NOTSRCCOPY = 0x00330008,
                NOTSRCERASE = 0x001100A6,
                MERGECOPY = 0x00C000CA,
                MERGEPAINT = 0x00BB0226,
                PATCOPY = 0x00F00021,
                PATPAINT = 0x00FB0A09,
                PATINVERT = 0x005A0049,
                DSTINVERT = 0x00550009,
                BLACKNESS = 0x00000042,
                WHITENESS = 0x00FF0062,
                CAPTUREBLT = 0x40000000
            }

            [StructLayout(LayoutKind.Sequential)]
            public class BITMAPINFOHEADER
            {
                public int biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                public int biWidth;
                public int biHeight;
                public short biPlanes;
                public short biBitCount;
                public BitmapCompressionMode biCompression;
                public int biSizeImage;
                public int biXPelsPerMeter;
                public int biYPelsPerMeter;
                public int biClrUsed;
                public int biClrImportant;
            }

            [StructLayout(LayoutKind.Sequential)]
            public class BITMAPINFO
            {
                public BITMAPINFOHEADER bmiHeader = new BITMAPINFOHEADER();
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
                public byte[] bmiColors;
            }

            public enum BitmapCompressionMode : uint
            {
                BI_RGB = 0,
                BI_RLE8 = 1,
                BI_RLE4 = 2,
                BI_BITFIELDS = 3,
                BI_JPEG = 4,
                BI_PNG = 5
            }

            public enum DIB_Color_Mode : uint
            {
                DIB_RGB_COLORS = 0,
                DIB_PAL_COLORS = 1
            }
        }
    }
}
