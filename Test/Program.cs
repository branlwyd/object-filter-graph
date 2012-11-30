using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using ObjectFilterGraph;
using ObjectFilterGraph.GenericFilters;
using ObjectFilterGraph.ImageFilters;

namespace Test
{
    class Program
    {
        //static void Main(string[] args)
        //{
        //    //GameOfLifeSource golSrc = new GameOfLifeSource(1680, 1050);
        //    GameOfLifeSource golSrc = new GameOfLifeSource(840, 525); // 1680 x 1050 / 2
        //    //GameOfLifeSource golSrc = new GameOfLifeSource(560, 350); // 1680 x 1050 / 3
        //    //GameOfLifeSource golSrc = new GameOfLifeSource(336, 210); // 1680 x 1050 / 5
        //    GenericDelay<Image> delayFilt = new GenericDelay<Image>(new TimeSpan(0, 0, 0, 0, 100));
        //    //WallpaperFilter wpFilt = new WallpaperFilter();
        //    DisplayFilter displayFilt = new DisplayFilter();

        //    golSrc.OutputPin.AttachTo(delayFilt.InputPin);
        //    delayFilt.OutputPin.AttachTo(displayFilt.InputPin);
        //    displayFilt.OutputPin.AttachTo(golSrc.InputPin);

        //    PinOut<Image> golSrcOut = new PinOut<Image>();
        //    golSrcOut.AttachTo(golSrc.InputPin);
        //    golSrcOut.Send(null);
        //    //golSrcOut.Send(null);
        //    //golSrcOut.Send(null);
        //    //golSrcOut.Send(null);
        //    Console.ReadLine();
        //}

        static void Main(string[] args)
        {
            RedditSource redditSource = new RedditSource("http://www.reddit.com/r/pics/.json");
            GenericFilter<Image> sizeFilt = new GenericFilter<Image>(img => 100 < img.Width && img.Width < 1000 && 100 < img.Height && img.Height < 800);
            BorderFilter brdInnerFilt = new BorderFilter(Color.Black, 1);
            BorderFilter brdOuterFilt = new BorderFilter(Color.White, 9);
            RotateFilter rotFilt = new RotateFilter(-30, 30);
            CanvasFilter cvsFilt = new CanvasFilter();
            GenericDelay<Image> delayFilt = new GenericDelay<Image>(new TimeSpan(0, 0, 30));
            WallpaperFilter wpFilt = new WallpaperFilter();

            redditSource.OutputPin.AttachTo(sizeFilt.InputPin);
            sizeFilt.SuccessPin.AttachTo(brdInnerFilt.InputPin);
            sizeFilt.FailurePin.AttachTo(redditSource.InputPin);
            brdInnerFilt.OutputPin.AttachTo(brdOuterFilt.InputPin);
            brdOuterFilt.OutputPin.AttachTo(rotFilt.InputPin);
            rotFilt.OutputPin.AttachTo(cvsFilt.PictureInputPin);
            cvsFilt.OutputPin.AttachTo(delayFilt.InputPin);
            cvsFilt.OutputPin.AttachTo(cvsFilt.CanvasInputPin);
            delayFilt.OutputPin.AttachTo(wpFilt.InputPin);
            wpFilt.OutputPin.AttachTo(redditSource.InputPin);

            /* prime canvas */
            PinOut<Image> canvasOut = new PinOut<Image>();
            canvasOut.AttachTo(cvsFilt.CanvasInputPin);
            Image canvas;
            try
            {
                int bytesRead;
                byte[] buf = new byte[1024];
                FileStream fs = new FileStream(Path.Combine(Environment.CurrentDirectory, "wall.bmp"), FileMode.Open);
                MemoryStream ms = new MemoryStream();
                while ((bytesRead = fs.Read(buf, 0, 1024)) > 0)
                    ms.Write(buf, 0, bytesRead);
                ms.Seek(0, SeekOrigin.Begin);
                fs.Close();
                canvas = Image.FromStream(ms);
            }
            catch (FileNotFoundException)
            {
                Size monitorSize = SystemInformation.PrimaryMonitorSize;
                canvas = new Bitmap(monitorSize.Width, monitorSize.Height);
            }
            canvasOut.Send(canvas);

            PinOut<Image> ljSrcOut = new PinOut<Image>();
            ljSrcOut.AttachTo(redditSource.InputPin);
            ljSrcOut.Send(null); /* only need a signal to kick off the source */
            ljSrcOut.Send(null); /* put two images in the loop to test multithreading */
            //ljSrcOut.Send(null); /* hell, why not three? */
            //ljSrcOut.Send(null); /* we are approaching levels of insanity heretofore untold */
            Console.ReadLine();
        }
    }
}
