using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;

namespace CryptoDataBase.Helpers
{
    public static class VideoHelper
    {
        public static Bitmap GetFrameWithWaterMark(string videoPath, double? targetSecond = null)
        {
            Bitmap frame = null;
            double duration = 0;

            // Спробуємо отримати вбудоване постер‑зображення за допомогою TagLib#
            try
            {
                var tagFile = TagLib.File.Create(videoPath);

                duration = tagFile.Properties.Duration.TotalSeconds;

                if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                {
                    var picture = tagFile.Tag.Pictures[0];
                    using (var ms = new MemoryStream(picture.Data.Data))
                    {
                        // Створюємо тимчасове зображення та повертаємо його копію
                        using (var tempImage = new Bitmap(ms))
                        {
                            frame = new Bitmap(tempImage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            if (frame == null || IsThumbTooSmall(frame))
            {
                string outputFileName = Path.GetDirectoryName(videoPath) + Path.DirectorySeparatorChar + Path.GetFileName(Path.GetTempFileName()) + ".bmp";

                try
                {
                    using (var engine = new Engine())
                    {
                        var inputFile = new MediaFile { Filename = videoPath };
                        engine.GetMetadata(inputFile);
                        duration = inputFile.Metadata.Duration.TotalSeconds;
                        double second = targetSecond ?? (duration > 10 ? 8 : Math.Min(1, duration));

                        var outputFile = new MediaFile { Filename = outputFileName };

                        try
                        {
                            engine.GetThumbnail(inputFile, outputFile, new ConversionOptions { Seek = TimeSpan.FromSeconds(second) });
                            using (var tempImage = new Bitmap(outputFileName))
                            {
                                frame = GetBiggest(new Bitmap(tempImage), frame);
                            }
                        }
                        catch { }

                        File.Delete(outputFileName);
                    }
                }
                catch { }
            }

            return DrawWaterMark(frame, (int)duration, Path.GetExtension(videoPath).Replace(".", ""));
        }

        public static Bitmap DrawWaterMark(Bitmap originalFrame, int length = 0, string format = null)
        {
            using (Graphics graphics = Graphics.FromImage(originalFrame))
            {
                string durationText = null;

                if (length > 0)
                {
                    int hours = (int)Math.Floor((double)length / 3600);
                    int minutes = (int)Math.Floor(((double)length - (hours * 3600)) / 60);
                    int seconds = length - (hours * 3600) - (minutes * 60);

                    durationText = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
                }

                // Налаштування якості рендерингу
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Налаштування шрифту
                Font font = new Font("Arial", originalFrame.Width / 8, FontStyle.Bold, GraphicsUnit.Pixel);

                SizeF formatTextSize = graphics.MeasureString(format, font);

                // Позиція тексту (центрування)
                float x = (originalFrame.Width - formatTextSize.Width);
                float y = (originalFrame.Height - formatTextSize.Height);

                graphics.FillRectangle(new SolidBrush(Color.DarkBlue), new Rectangle((int)x - 5, (int)y - 5, (int)formatTextSize.Width + 5, (int)formatTextSize.Height + 5));
                DrawText(graphics, format, font, x, y);

                // Розміри тексту
                if (durationText != null)
                {
                    SizeF durationTextSize = graphics.MeasureString(durationText, font);

                    y = (originalFrame.Height - durationTextSize.Height);
                    DrawText(graphics, durationText, font, 0, y);
                }
            }

            return originalFrame;
        }

        private static void DrawText(Graphics graphics, string text, Font font, float x, float y)
        {
            // Додаємо тінь
            graphics.DrawString(
                text,
                font,
                Brushes.Black,
                new PointF(x + 5, y + 5) // Зсув для ефекту тіні
            );

            // Додаємо основний текст
            graphics.DrawString(
                text,
                font,
                Brushes.White,
                new PointF(x, y)
            );
        }

        private static bool IsThumbTooSmall(Bitmap bitmap)
        {
            return (bitmap.Width * bitmap.Height) < (10000);
        }

        private static Bitmap GetBiggest(Bitmap bitmap1, Bitmap bitmap2)
        {
            if (bitmap1 == null)
            {
                return bitmap2;
            }

            if (bitmap2 == null)
            {
                return bitmap1;
            }

            return bitmap1.Width * bitmap1.Height > bitmap2.Width * bitmap2.Height ? bitmap1 : bitmap2;
        }
    }
}
