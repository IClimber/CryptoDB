using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoDataBase.Controls
{
    public class ImageView : Control
    {
        public const int STANDART_DPI = 96;
        protected Image image;

        public bool IsStretch { get { return isStretch; } set { isStretch = value; InitImageSize(); } }

        private bool isStretch;
        private Point origin;  // Original Offset of image
        private Point start;   // Original Position of the mouse
        private int rotate = 0;
        private double Zoom = 1;
        private double ZoomStep = 1.1;
        private Transform originalTransform;
        private readonly int Dpi = STANDART_DPI;
        private BitmapImage bitmap = null;

        static ImageView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageView), new FrameworkPropertyMetadata(typeof(ImageView)));
        }

        public ImageView()
        {
            Dpi = (int)(STANDART_DPI * (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth));
        }

        public override void OnApplyTemplate()
        {
            image = (Image)Template.FindName("PART_image", this);

            image.MouseLeftButtonDown += OnImageMouseLeftButtonDown;
            image.MouseLeftButtonUp += OnImageMouseLeftButtonUp;
            image.MouseMove += OnImageMouseMove;

            base.OnApplyTemplate();
        }

        public void SetSource(BitmapImage bitmap)
        {
            image.Width = 0;
            image.Height = 0;
            image.RenderTransform = new ScaleTransform();
            rotate = 0;
            image.Stretch = Stretch.None;
            Zoom = 1;

            this.bitmap = bitmap;
            image.Source = this.bitmap;

            image.Stretch = Stretch.Uniform;
            originalTransform = image.RenderTransform;
            InitImageSize();
        }

        public void Rotate90()
        {
            Matrix m = image.RenderTransform.Value;
            m.RotateAt(90, image.RenderSize.Width / 2, image.RenderSize.Height / 2);
            image.RenderTransform = new MatrixTransform(m);

            rotate += 90;
            rotate = rotate > 270 ? 0 : rotate;

            InitImageSize();
        }

        public void Rotate270()
        {
            Matrix m = image.RenderTransform.Value;
            m.RotateAt(270, image.RenderSize.Width / 2, image.RenderSize.Height / 2);
            image.RenderTransform = new MatrixTransform(m);

            rotate -= 90;
            rotate = rotate < 0 ? 270 : rotate;

            InitImageSize();
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            Point p = e.MouseDevice.GetPosition(image);

            Matrix m = image.RenderTransform.Value;
            if (e.Delta > 0)
            {
                if ((Zoom * ZoomStep) <= 10)
                {
                    Zoom *= ZoomStep;
                    m.ScaleAtPrepend(ZoomStep, ZoomStep, p.X, p.Y);
                }
            }
            else
            {
                if ((Zoom / ZoomStep) >= 1)
                {
                    Zoom /= ZoomStep;
                    m.ScaleAtPrepend(1 / ZoomStep, 1 / ZoomStep, p.X, p.Y);
                }
                else
                {
                    Zoom = 1;

                    m = originalTransform.Value;
                    m.RotateAt(rotate, image.RenderSize.Width / 2, image.RenderSize.Height / 2);
                }
            }
            image.RenderTransform = new MatrixTransform(m);

            SetOffset(m.OffsetX, m.OffsetY);
        }

        private void OnImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (image.IsMouseCaptured)
            {
                return;
            }
            image.CaptureMouse();


            start = e.GetPosition(this);
            origin.X = image.RenderTransform.Value.OffsetX;
            origin.Y = image.RenderTransform.Value.OffsetY;
        }

        private void OnImageMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            image.ReleaseMouseCapture();
        }

        private void OnImageMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!image.IsMouseCaptured) return;
            Point p = e.MouseDevice.GetPosition(this);

            double XOffset = origin.X + (p.X - start.X);
            double YOffset = origin.Y + (p.Y - start.Y);
            SetOffset(XOffset, YOffset);
        }

        private double getOriginalDpiX()
        {
            return bitmap.DpiX > 0 ? bitmap.DpiX : STANDART_DPI;
        }

        private double getOriginalDpiY()
        {
            return bitmap.DpiY > 0 ? bitmap.DpiY : STANDART_DPI;
        }

        private double CalculateImageWidth()
        {
            return image.Source.Width * getOriginalDpiX() / Dpi;
        }

        private double CalculateImageHeight()
        {
            return image.Source.Height * getOriginalDpiY() / Dpi;
        }

        private void SetOffset(double? OffsetX = null, double? OffsetY = null)
        {
            Matrix m = image.RenderTransform.Value;
            OffsetX = OffsetX ?? m.OffsetX;
            OffsetY = OffsetY ?? m.OffsetY;

            double imageWidth = image.Width;
            double imageHeight = image.Height;
            double borderWidth = RenderSize.Width;
            double borderHeight = RenderSize.Height;
            double a = imageWidth / CalculateImageWidth();
            double b = imageHeight / CalculateImageHeight();
            if (a < b)
            {
                imageHeight = CalculateImageHeight() * a;
            }
            else
            {
                imageWidth = CalculateImageWidth() * b;
            }

            if (rotate == 90 || rotate == 270)
            {
                var x = imageWidth;
                imageWidth = imageHeight;
                imageHeight = x;
            }

            var imgXMinCenter = ((borderWidth - imageWidth * Zoom) / 2) - ((borderWidth - imageWidth) / 2);
            var imgXMaxCenter = imgXMinCenter;
            var imgYMinCenter = ((borderHeight - imageHeight * Zoom) / 2) - ((borderHeight - imageHeight) / 2);
            var imgYMaxCenter = imgYMinCenter;

            if ((imageWidth * Zoom) > RenderSize.Width)
            {
                imgXMinCenter = -((imageWidth * Zoom - borderWidth) + (borderWidth - imageWidth) / 2);
                imgXMaxCenter = -((borderWidth - imageWidth) / 2);
            }

            if ((imageHeight * Zoom) > borderHeight)
            {
                imgYMinCenter = -((imageHeight * Zoom - borderHeight) + (borderHeight - imageHeight) / 2);
                imgYMaxCenter = -((borderHeight - imageHeight) / 2);
            }

            double xOffset = 0;
            double yOffset = 0;
            double minXCenter = imgXMinCenter;
            double maxXCenter = imgXMaxCenter;
            double minYCenter = imgYMinCenter;
            double maxYCenter = imgYMaxCenter;

            switch (rotate)
            {
                case 90:
                    {
                        xOffset = imageHeight + (imageWidth - imageHeight) / 2;
                        yOffset = (imageWidth - imageHeight) / 2;
                        minXCenter = -imgXMaxCenter;
                        maxXCenter = -imgXMinCenter;
                        break;
                    }
                case 180:
                    {
                        xOffset = imageWidth;
                        yOffset = imageHeight;
                        minXCenter = -imgXMaxCenter;
                        maxXCenter = -imgXMinCenter;
                        minYCenter = -imgYMaxCenter;
                        maxYCenter = -imgYMinCenter;
                        break;
                    }
                case 270:
                    {
                        xOffset = (imageHeight - imageWidth) / 2;
                        yOffset = imageWidth + (imageHeight - imageWidth) / 2;
                        minYCenter = -imgYMaxCenter;
                        maxYCenter = -imgYMinCenter;
                        break;
                    }
            }

            double minOffsetX = xOffset + minXCenter;
            double maxOffsetX = xOffset + maxXCenter;
            double minOffsetY = yOffset + minYCenter;
            double maxOffsetY = yOffset + maxYCenter;

            OffsetX = OffsetX < minOffsetX ? minOffsetX : OffsetX;
            OffsetX = OffsetX > maxOffsetX ? maxOffsetX : OffsetX;

            OffsetY = OffsetY < minOffsetY ? minOffsetY : OffsetY;
            OffsetY = OffsetY > maxOffsetY ? maxOffsetY : OffsetY;

            m.OffsetX = (double)OffsetX;
            m.OffsetY = (double)OffsetY;
            image.RenderTransform = new MatrixTransform(m);
        }

        private void InitImageSize()
        {
            try
            {
                double imgWidth = CalculateImageWidth();
                double imgHeight = CalculateImageHeight();
                double imgRotatedWidth = imgWidth;
                double imgRotatedHeight = imgHeight;
                bool needSwap = (rotate == 90 || rotate == 270);

                if (needSwap)
                {
                    (imgRotatedHeight, imgRotatedWidth) = (imgRotatedWidth, imgRotatedHeight);
                }

                if (isStretch || (imgRotatedWidth > RenderSize.Width) || (imgRotatedHeight > RenderSize.Height))
                {
                    imgWidth = RenderSize.Width;
                    imgHeight = RenderSize.Height;

                    if (needSwap)
                    {
                        (imgHeight, imgWidth) = (imgWidth, imgHeight);
                    }
                }

                image.Width = imgWidth;
                image.Height = imgHeight;
            }
            catch
            { }

            SetOffset();
        }
    }
}
