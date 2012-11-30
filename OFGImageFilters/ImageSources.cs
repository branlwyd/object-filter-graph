//#define DEBUG_TIMING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Xml;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObjectFilterGraph;

namespace ObjectFilterGraph.ImageFilters
{
    public class GameOfLifeSource : ObjectSource<Image>
    {
        private const double DENSITY_RANDOMIZE_STATE = 0.5;
        private const double DENSITY_BIOME_ADD = 0.25;
        private const int CYCLES_BETWEEN_BIOMES = 60;
        private const double MIN_BIOME_RADIUS = 150;
        private const double MAX_BIOME_RADIUS = 250;
        private const int INITIAL_BIOMES = 0;
        private const bool INITIAL_RANDOMIZE = true;

        private Random rand;
        private bool[] state;
        private byte[] colorMap;
        private int[][] neighbors;
        private int width;
        private int height;
        private int cyclesBeforeBiome;
        private double[] hrzDistMap;
        private double[] vrtDistMap;

        public GameOfLifeSource(int width, int height) : base()
        {
            this.rand = new Random();
            this.width = width;
            this.height = height;
            this.state = new bool[width * height];
            this.colorMap = new byte[3 * width * height];
            this.cyclesBeforeBiome = CYCLES_BETWEEN_BIOMES;
            this.hrzDistMap = new double[width];
            this.vrtDistMap = new double[height];

            this.neighbors = computeNeighbors();
            computeSqrDist(this.hrzDistMap);
            computeSqrDist(this.vrtDistMap);
            

            if (INITIAL_RANDOMIZE)
                RandomizeState(false);
            for (int i = 0; i < INITIAL_BIOMES; ++i)
                AddBiome(false);
        }

        private int[][] computeNeighbors()
        {
            int[][] neighbors = new int[width * height][];

            for(int i = 0; i < width; ++i)
                for (int j = 0; j < height; ++j)
                {
                    neighbors[width * j + i] = new int[8];
                    int neighborsIdx = 0;

                    for (int dx = -1; dx <= 1; ++dx)
                        for (int dy = -1; dy <= 1; ++dy)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int targX = i + dx;
                            int targY = j + dy;

                            if (targX == -1) targX = width - 1;
                            else if (targX == width) targX = 0;
                            if (targY == -1) targY = height - 1;
                            else if (targY == height) targY = 0;

                            int target = width * targY + targX;

                            neighbors[width * j + i][neighborsIdx] = target;
                            neighborsIdx++;
                        }
                }

            return neighbors;
        }

        private void computeSqrDist(double[] dst)
        {
            for (int i = 0; i < dst.Length; ++i)
            {
                if (i <= dst.Length / 2)
                    dst[i] = i * i;
                else
                    dst[i] = (i - dst.Length) * (i - dst.Length);
            }
        }

        private Image RandomizeState(bool makeImage = true)
        {
            byte[] newColorMap;

            lock (this)
            {
#if DEBUG_TIMING
                DateTime startTime = DateTime.Now;
#endif

                newColorMap = colorMap;
                rand.NextBytes(newColorMap);
                for (int i = 0; i < width * height; ++i)
                {
                    bool live = (rand.NextDouble() <= DENSITY_RANDOMIZE_STATE);
                    state[i] = live;
                    if(!live)
                        newColorMap[3 * i] = newColorMap[3 * i + 1] = newColorMap[3 * i + 2] = 0;
                }

#if DEBUG_TIMING
                DateTime endTime = DateTime.Now;
                if (makeImage)
                {
                    TimeSpan time = endTime - startTime;
                    Console.Error.WriteLine("RandomizeState: {0}", time.TotalMilliseconds);
                }
#endif
            }

            return (makeImage ? MakeImageFromState(newColorMap) : null);
        }

        private Image AddBiome(bool makeImage = true)
        {
            byte[] newColorMap;

            lock (this)
            {
#if DEBUG_TIMING
                DateTime startTime = DateTime.Now;
#endif

                newColorMap = colorMap;
                int locX = (int)(width * rand.NextDouble());
                int locY = (int)(height * rand.NextDouble());
                double r = MIN_BIOME_RADIUS + (MAX_BIOME_RADIUS - MIN_BIOME_RADIUS) * rand.NextDouble();
                byte[] color = new byte[3];

                int offsetX = width - locX;
                int offsetY = height - locY;
                double[] curHrzDistMap = new double[width];
                double[] curVrtDistMap = new double[height];
                for(int i = 0; i < width; ++i)
                    curHrzDistMap[i] = hrzDistMap[(i + offsetX) % width];
                for(int j = 0; j < height; ++j)
                    curVrtDistMap[j] = vrtDistMap[(j + offsetY) % height];

                r = r * r;

                for (int i = 0; i < width; ++i)
                    for (int j = 0; j < height; ++j)
                    {
                        if (curHrzDistMap[i] + curVrtDistMap[j] > r)
                            continue;

                        if (rand.NextDouble() <= DENSITY_BIOME_ADD)
                        {
                            state[width * j + i] = true;
                            rand.NextBytes(color);
                            newColorMap[3 * (width * j + i)] = color[0];
                            newColorMap[3 * (width * j + i) + 1] = color[1];
                            newColorMap[3 * (width * j + i) + 2] = color[2];
                        }
                    }

#if DEBUG_TIMING
                DateTime endTime = DateTime.Now;
                if (makeImage)
                {
                    TimeSpan time = endTime - startTime;
                    Console.Error.WriteLine("AddBiome: {0}", time.TotalMilliseconds);
                }
#endif
            }

            return (makeImage ? MakeImageFromState(newColorMap) : null);
        }

        private Image UpdateState(bool makeImage = true)
        {
            bool[] newState = new bool[width * height];
            byte[] newColorMap = new byte[3 * width * height];

            lock (this)
            {
#if DEBUG_TIMING
                DateTime startTime = DateTime.Now;
#endif
                for (int i = 0; i < width * height; ++i)
                {
                    bool live = state[i];
                    int[] curNeighbors = neighbors[i];
                    int liveNeighbors = 0;

                    if (state[curNeighbors[0]]) liveNeighbors++;
                    if (state[curNeighbors[1]]) liveNeighbors++;
                    if (state[curNeighbors[2]]) liveNeighbors++;
                    if (state[curNeighbors[3]]) liveNeighbors++;
                    if (state[curNeighbors[4]]) liveNeighbors++;
                    if (state[curNeighbors[5]]) liveNeighbors++;
                    if (state[curNeighbors[6]]) liveNeighbors++;
                    if (state[curNeighbors[7]]) liveNeighbors++;

                    bool newLive = (live && (liveNeighbors == 2)) || (liveNeighbors == 3);
                    newState[i] = newLive;
                    if (!newLive)
                    {
                        newColorMap[3 * i] = 0;
                        newColorMap[3 * i + 1] = 0;
                        newColorMap[3 * i + 2] = 0;
                    }
                    else if (live && newLive)
                    {
                        newColorMap[3 * i] = colorMap[3 * i];
                        newColorMap[3 * i + 1] = colorMap[3 * i + 1];
                        newColorMap[3 * i + 2] = colorMap[3 * i + 2];
                    }
                    else if (!live && newLive)
                    {
                        int avgBlue = 0;
                        int avgGreen = 0;
                        int avgRed = 0;

                        if (state[curNeighbors[0]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[0]];
                            avgGreen += colorMap[3 * curNeighbors[0] + 1];
                            avgRed += colorMap[3 * curNeighbors[0] + 2];
                        }
                        if (state[curNeighbors[1]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[1]];
                            avgGreen += colorMap[3 * curNeighbors[1] + 1];
                            avgRed += colorMap[3 * curNeighbors[1] + 2];
                        }
                        if (state[curNeighbors[2]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[2]];
                            avgGreen += colorMap[3 * curNeighbors[2] + 1];
                            avgRed += colorMap[3 * curNeighbors[2] + 2];
                        }
                        if (state[curNeighbors[3]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[3]];
                            avgGreen += colorMap[3 * curNeighbors[3] + 1];
                            avgRed += colorMap[3 * curNeighbors[3] + 2];
                        }
                        if (state[curNeighbors[4]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[4]];
                            avgGreen += colorMap[3 * curNeighbors[4] + 1];
                            avgRed += colorMap[3 * curNeighbors[4] + 2];
                        }
                        if (state[curNeighbors[5]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[5]];
                            avgGreen += colorMap[3 * curNeighbors[5] + 1];
                            avgRed += colorMap[3 * curNeighbors[5] + 2];
                        }
                        if (state[curNeighbors[6]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[6]];
                            avgGreen += colorMap[3 * curNeighbors[6] + 1];
                            avgRed += colorMap[3 * curNeighbors[6] + 2];
                        }
                        if (state[curNeighbors[7]])
                        {
                            avgBlue += colorMap[3 * curNeighbors[7]];
                            avgGreen += colorMap[3 * curNeighbors[7] + 1];
                            avgRed += colorMap[3 * curNeighbors[7] + 2];
                        }

                        // round
                        if (avgBlue % 3 == 2) avgBlue++;
                        if (avgGreen % 3 == 2) avgGreen++;
                        if (avgRed % 3 == 2) avgRed++;

                        avgBlue /= 3;
                        avgGreen /= 3;
                        avgRed /= 3;

                        newColorMap[3 * i] = (byte)avgBlue;
                        newColorMap[3 * i + 1] = (byte)avgGreen;
                        newColorMap[3 * i + 2] = (byte)avgRed;
                    }
                }

                state = newState;
                colorMap = newColorMap;

#if DEBUG_TIMING
                DateTime endTime = DateTime.Now;
                TimeSpan time = endTime - startTime;
                Console.Error.WriteLine("UpdateState: {0}", time.TotalMilliseconds);
#endif
            }

            return (makeImage ? MakeImageFromState(newColorMap) : null);
        }

        private Image MakeImageFromState(byte[] colorMap)
        {
#if DEBUG_TIMING
            DateTime startTime = DateTime.Now;
#endif

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            System.Runtime.InteropServices.Marshal.Copy(colorMap, 0, bmpData.Scan0, colorMap.Length);
            bmp.UnlockBits(bmpData);


#if DEBUG_TIMING
            DateTime endTime = DateTime.Now;
            TimeSpan time = endTime - startTime;
            Console.Error.WriteLine("MakeImageFromState: {0}", time.TotalMilliseconds);
#endif

            return bmp;
        }

        public override Image Generate()
        {
            Image img;

            if (cyclesBeforeBiome == 0)
            {
                cyclesBeforeBiome = CYCLES_BETWEEN_BIOMES;
                img = AddBiome();
            }
            else
            {
                cyclesBeforeBiome--;
                img = UpdateState();
            }

            return img;
        }
    }

    public class RedditSource : ObjectSource<Image>
    {
        private Queue<Uri> uris;
        private string url;

        private const string USER_AGENT = "ObjectFilterGraph.ImageFilters.RedditSource; brandon.pitman@gmail.com";

        public RedditSource(string url) : base()
        {
            /* url in the format: http://www.reddit.com/r/subreddit/.json/ */
            this.url = url;

            uris = new Queue<Uri>();
        }

        private void GetMoreUris()
        {
            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(this.url);
            req.UserAgent = USER_AGENT;
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            string json = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            JObject jsonObj = JObject.Parse(json);

            var urls =
                from c in jsonObj["data"]["children"].Children()
                select c["data"]["url"].Value<string>();

            foreach (string url in urls)
            {
                try
                {
                    uris.Enqueue(new Uri(url));
                }
                catch (UriFormatException) { }
            }
        }

        public override Image Generate()
        {
            do
            {
            NewImage:
                try
                {
                    Uri uri;

                    lock (this)
                    {
                        while (uris.Count == 0)
                            GetMoreUris();
                        uri = uris.Dequeue();
                    }

                    HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(uri);
                    req.UserAgent = USER_AGENT;
                    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                    Stream respStream = resp.GetResponseStream();
                    long totalLength = resp.ContentLength;

                    MemoryStream imgStream;
                    if (totalLength != -1)
                    {
                        int bytesRead = 0;
                        byte[] imgBytes = new byte[resp.ContentLength];

                        while (bytesRead < totalLength)
                        {
                            int bytesThisTime = respStream.Read(imgBytes, bytesRead, (int)(totalLength - bytesRead));
                            if (bytesThisTime == 0)
                            {
                                /* premature end of stream -- bail on this image */
                                respStream.Close();
                                goto NewImage;
                            }
                            bytesRead += bytesThisTime;
                        }

                        imgStream = new MemoryStream(imgBytes);
                    }
                    else
                    {
                        /* content length header not set; just read until end of stream */
                        imgStream = new MemoryStream();
                        byte[] imgBytes = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = respStream.Read(imgBytes, 0, 1024)) != 0)
                            imgStream.Write(imgBytes, 0, bytesRead);
                        imgStream.Seek(0, SeekOrigin.Begin);
                    }

                    respStream.Close();
                    Image img = Image.FromStream(imgStream, true, true);

                    if ((img.PixelFormat & PixelFormat.Indexed) > 0
                        || img.PixelFormat == PixelFormat.Format16bppArgb1555
                        || img.PixelFormat == PixelFormat.Format16bppGrayScale)
                    {
                        /* Graphics.FromImage doesn't work on these types; convert */
                        imgStream = new MemoryStream();
                        img.Save(imgStream, ImageFormat.MemoryBmp);
                        img = Image.FromStream(imgStream, true, true);
                    }

                    /* this code is a last-ditch fallthrough to convert images which can't have FromGraphics called on them... */
                    /* XXX: should be removed once all FromImage failure conditions are found */
                    /* XXX: all this image scrubbing functionality should be factored out of this specific class */
                    try
                    {
                        Graphics g = Graphics.FromImage(img);
                        g.Dispose();
                    }
                    catch (OutOfMemoryException)
                    {
                        Console.WriteLine("needed second conversion: {0}", uri);
                        imgStream = new MemoryStream();
                        img.Save(imgStream, ImageFormat.MemoryBmp);
                        img = Image.FromStream(imgStream, true, true);
                    }

                    return img;
                }
                catch (WebException) { } /* from req.GetResponse */
                catch (ArgumentException) { } /* from Image.FromStream */
                catch (IOException) { } /* from various stream Reads and Writes */
            } while (true);
        }
    }

    public class LiveJournalSource : ObjectSource<Image>
    {
        private Queue<Uri> uris;

        private const string RECENT_IMAGES_URI = "http://www.livejournal.com/stats/latest-img.bml";
        private const string USER_AGENT = "ObjectFilterGraph.ImageFilters.LiveJournalSource; brandon.pitman@gmail.com";

        public LiveJournalSource() : base()
        {
            uris = new Queue<Uri>();
        }

        private void GetMoreUris()
        {
            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(RECENT_IMAGES_URI);
            req.UserAgent = USER_AGENT;
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            XmlDocument imgXml = new XmlDocument();
            imgXml.Load(resp.GetResponseStream());
            XmlNodeList recentImages = imgXml.SelectNodes("/livejournal/recent-images/recent-image");

            foreach (XmlNode recentImage in recentImages)
            {
                try
                {
                    uris.Enqueue(new Uri(recentImage.Attributes["img"].InnerText));
                }
                catch (UriFormatException) { }
            }
        }

        public override Image Generate()
        {
            do
            {
            NewImage:
                try
                {
                    Uri uri;

                    lock (this)
                    {
                        while (uris.Count == 0)
                            GetMoreUris();
                        uri = uris.Dequeue();
                    }

                    HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(uri);
                    req.UserAgent = USER_AGENT;
                    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                    Stream respStream = resp.GetResponseStream();
                    long totalLength = resp.ContentLength;

                    MemoryStream imgStream;
                    if (totalLength != -1)
                    {
                        int bytesRead = 0;
                        byte[] imgBytes = new byte[resp.ContentLength];

                        while (bytesRead < totalLength)
                        {
                            int bytesThisTime = respStream.Read(imgBytes, bytesRead, (int)(totalLength - bytesRead));
                            if (bytesThisTime == 0)
                            {
                                /* premature end of stream -- bail on this image */
                                respStream.Close();
                                goto NewImage;
                            }
                            bytesRead += bytesThisTime;
                        }

                        imgStream = new MemoryStream(imgBytes);
                    }
                    else
                    {
                        /* content length header not set; just read until end of stream */
                        imgStream = new MemoryStream();
                        byte[] imgBytes = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = respStream.Read(imgBytes, 0, 1024)) != 0)
                            imgStream.Write(imgBytes, 0, bytesRead);
                        imgStream.Seek(0, SeekOrigin.Begin);
                    }

                    respStream.Close();
                    Image img = Image.FromStream(imgStream, true, true);

                    if ((img.PixelFormat & PixelFormat.Indexed) > 0
                        || img.PixelFormat == PixelFormat.Format16bppArgb1555
                        || img.PixelFormat == PixelFormat.Format16bppGrayScale)
                    {
                        /* Graphics.FromImage doesn't work on these types; convert */
                        imgStream = new MemoryStream();
                        img.Save(imgStream, ImageFormat.MemoryBmp);
                        img = Image.FromStream(imgStream, true, true);
                    }

                    /* this code is a last-ditch fallthrough to convert images which can't have FromGraphics called on them... */
                    /* XXX: should be removed once all FromImage failure conditions are found */
                    /* XXX: all this image scrubbing functionality should be factored out of this specific class */
                    try
                    {
                        Graphics g = Graphics.FromImage(img);
                        g.Dispose();
                    }
                    catch (OutOfMemoryException)
                    {
                        Console.WriteLine("needed second conversion: {0}", uri);
                        imgStream = new MemoryStream();
                        img.Save(imgStream, ImageFormat.MemoryBmp);
                        img = Image.FromStream(imgStream, true, true);
                    }

                    return img;
                }
                catch (WebException) { } /* from req.GetResponse */
                catch (ArgumentException) { } /* from Image.FromStream */
                catch (IOException) { } /* from various stream Reads and Writes */
            } while (true);
        }
    }
}
