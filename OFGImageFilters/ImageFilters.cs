using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace ObjectFilterGraph.ImageFilters
{
    public class BorderFilter : IObjectFilter<Image>
    {
        public PinIn<Image> InputPin;
        public PinOut<Image> OutputPin;

        private Color borderColor;
        private int borderSize;

        public BorderFilter(Color borderColor, int borderSize)
        {
            if (borderSize < 0)
                throw new ArgumentException("borderWidth must be positive");

            InputPin = new PinIn<Image>(this);
            OutputPin = new PinOut<Image>();

            this.borderColor = borderColor;
            this.borderSize = borderSize;
        }

        public void Receive(PinIn<Image> pin, Image img)
        {
            Graphics gOld = Graphics.FromImage(img);
            Bitmap outImage = new Bitmap(img.Width + 2 * borderSize, img.Height + 2 * borderSize, gOld);
            gOld.Dispose();
            Graphics g = Graphics.FromImage(outImage);
            g.Clear(borderColor);
            g.DrawImage(img, new Point(borderSize, borderSize));
            g.Dispose();

            OutputPin.Send(outImage);
        }
    }

    public class RotateFilter : IObjectFilter<Image>
    {
        public PinIn<Image> InputPin;
        public PinOut<Image> OutputPin;

        private float minAngle;
        private float maxAngle;
        private Random random;

        public RotateFilter(float minAngle, float maxAngle)
        {
            InputPin = new PinIn<Image>(this);
            OutputPin = new PinOut<Image>();
            random = new Random();

            this.minAngle = minAngle;
            this.maxAngle = maxAngle;
        }

        public void Receive(PinIn<Image> pin, Image img)
        {
            /* choose an angle */
            double angle = minAngle + (maxAngle - minAngle) * random.NextDouble();
            double angleRadians = -angle * Math.PI / 180; /* 

            /* figure out new width and height as well as appropriate place to put rotated image */
            double s = Math.Sin(angleRadians);
            double c = Math.Cos(angleRadians);
            double newWidth = img.Width * Math.Abs(c) + img.Height * Math.Abs(s);
            double newHeight = img.Height * Math.Abs(c) + img.Width * Math.Abs(s);
            double minX = Math.Min(0, Math.Min(c * img.Width, Math.Min(s * img.Height, c * img.Width + s * img.Height)));
            double minY = Math.Min(0, Math.Min(-s * img.Width, Math.Min(c * img.Height, c * img.Height - s * img.Width)));

            /* create the rotated image */
            Graphics gOld = Graphics.FromImage(img);
            Image outImage = new Bitmap(1 + (int)newWidth, 1 + (int)newHeight, gOld);
            gOld.Dispose();
            Graphics g = Graphics.FromImage(outImage);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(-(float)minX + 1, -(float)minY + 1);
            g.RotateTransform((float)angle);
            g.DrawImage(img, 0, 0);
            g.Dispose();

            OutputPin.Send(outImage);
        }
    }

    public class CanvasFilter : IObjectFilter<Image>
    {
        public PinIn<Image> PictureInputPin;
        public PinIn<Image> CanvasInputPin;
        public PinOut<Image> OutputPin;

        private Image pic;
        private Image canvas;
        private Random random;

        public CanvasFilter()
        {
            PictureInputPin = new PinIn<Image>(this);
            CanvasInputPin = new PinIn<Image>(this);
            OutputPin = new PinOut<Image>();

            pic = null;
            canvas = null;
            random = new Random();
        }

        public void Receive(PinIn<Image> pin, Image item)
        {
            bool timeToOutput = false;
            Image myPic = null;
            Image myCanvas = null;

            lock (this)
            {
                if (pin == PictureInputPin)
                    pic = item;
                if (pin == CanvasInputPin)
                    canvas = item;

                if (pic != null && pin != null)
                {
                    timeToOutput = true;
                    myPic = pic;
                    myCanvas = canvas;
                    pic = null;
                    canvas = null;
                }
            }

            if (timeToOutput)
            {
                Graphics g = Graphics.FromImage(myCanvas);

                double X = myCanvas.Width  * random.NextDouble() - myPic.Width / 2; /* [-pic.width/2, canvas.width - pic.width/2] */
                double Y = myCanvas.Height * random.NextDouble() - myPic.Height / 2; /* ditto but for height */

                g.DrawImage(myPic, (float)X, (float)Y);
                g.Dispose();

                OutputPin.Send(myCanvas);
            }
        }
    }

    public class WallpaperFilter : IObjectFilter<Image>
    {
        public PinIn<Image> InputPin;
        public PinOut<Image> OutputPin;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_SENDCHANGE = 0x2;

        private bool SetWallpaper(string filename)
        {
            return (SystemParametersInfo(SPI_SETDESKWALLPAPER, 1, filename, SPIF_SENDCHANGE) != 0);
        }

        public WallpaperFilter()
        {
            InputPin = new PinIn<Image>(this);
            OutputPin = new PinOut<Image>();
        }

        public void Receive(PinIn<Image> pin, Image img)
        {
            string wallName = Path.Combine(Environment.CurrentDirectory, "wall.bmp");
            lock (this)
            {
                FileStream wallStream = new FileStream(wallName, FileMode.OpenOrCreate);
                img.Save(wallStream, ImageFormat.Bmp);
                wallStream.Close();
                SetWallpaper(wallName);
            }

            OutputPin.Send(img);
        }
    }

    public class DisplayFilter : IObjectFilter<Image>
    {
        public PinIn<Image> InputPin;
        public PinOut<Image> OutputPin;
        
        private DisplayFilterForm dff;

        public DisplayFilter()
        {
            InputPin = new PinIn<Image>(this);
            OutputPin = new PinOut<Image>();

            dff = new DisplayFilterForm();

            Thread thr = new Thread(new ParameterizedThreadStart(DispForm));
            thr.IsBackground = true;
            thr.Start(dff);
        }

        public void Receive(PinIn<Image> pin, Image img)
        {
            lock (this)
            {
                dff.SetPicture(img);
            }

            OutputPin.Send(img);
        }

        private void DispForm(object obj)
        {
            Form frm = (Form)obj;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(frm);
        }
    }
}
