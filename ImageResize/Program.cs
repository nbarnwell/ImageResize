using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;

namespace ImageResize
{
    class Program
    {
        static void Main(string[] args)
        {
            var linearRunTimes = new List<long>();
            var parallelRunTimes = new List<long>();
            const int runs = 10;

            for (int i = 0; i < runs + 1; i++)
            {
                Console.WriteLine("Starting linear run {0} --------------------------------------------------------", i);

                var resizingQueue = new BlockingCollection<ImageContainer>();
                var savingQueue = new BlockingCollection<ImageContainer>();

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                LoadFiles(resizingQueue);
                ResizeImages(resizingQueue, savingQueue);
                SaveFiles(savingQueue);
                stopwatch.Stop();
                linearRunTimes.Add(stopwatch.ElapsedMilliseconds);
            }            

            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");

            for (int i = 0; i < runs + 1; i++)
            {
                Console.WriteLine("Starting parallel run {0} --------------------------------------------------------", i);

                var resizingQueue = new BlockingCollection<ImageContainer>();
                var savingQueue = new BlockingCollection<ImageContainer>();
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                Parallel.Invoke(
                    () =>
                    {
                        LoadFiles(resizingQueue);
                    },
                    () =>
                    {
                        ResizeImages(resizingQueue, savingQueue);
                    },
                    () =>
                    {
                        SaveFiles(savingQueue);
                    }
                    );
                stopwatch.Stop();
                parallelRunTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            Console.WriteLine("Averages over {0} runs: Linear = {1}ms, Parallel = {2}ms", runs, linearRunTimes.Skip(1).Average(), parallelRunTimes.Skip(1).Average());

            Console.WriteLine("Completed. Press any key to close...");
            Console.ReadKey();
        }

        private static void SaveFiles(BlockingCollection<ImageContainer> savingQueue)
        {
            Console.WriteLine("Starting saving task...");
            Parallel.ForEach(savingQueue.GetConsumingEnumerable(), imageContainer =>
            {
                Console.WriteLine("   Saving {0}...", imageContainer.Filename);
                var image = imageContainer.Image;

                using (var stream = new FileStream(Path.Combine(@"C:\temp\Images\2011-09-18-resized", imageContainer.Filename + ".png"), FileMode.Create))
                {
                    image.Save(stream, ImageFormat.Png);
                }
            });

            Console.WriteLine("Completed saving task.");
        }

        private static void ResizeImages(BlockingCollection<ImageContainer> resizingQueue, BlockingCollection<ImageContainer> savingQueue)
        {
            Console.WriteLine("Starting resizing task...");
            Parallel.ForEach(resizingQueue.GetConsumingEnumerable(), imageContainer =>
            {
                Console.WriteLine("   Resizing {0}...", imageContainer.Filename);
                Image image = imageContainer.Image;
                int percentSize = 10;
                var smallerImage = new Bitmap(image.Width / percentSize, image.Height / percentSize);
                using (var g = Graphics.FromImage(smallerImage))
                {
                    g.DrawImage(image, new Rectangle(0, 0, image.Width / percentSize, image.Height / percentSize));
                }
                Thread.Sleep(2000);
                savingQueue.Add(new ImageContainer { Image = smallerImage, Filename = imageContainer.Filename });
            });
            savingQueue.CompleteAdding();
            Console.WriteLine("Completed resizing task.");
        }

        private static void LoadFiles(BlockingCollection<ImageContainer> resizingQueue)
        {
            Console.WriteLine("Starting loading task...");
            var files = Directory.GetFiles(@"C:\temp\Images\2011-09-18", "*.jpg");
            Parallel.ForEach(files, qualifiedFilename =>
            {
                var filenameWithoutExtension = Path.GetFileNameWithoutExtension(qualifiedFilename);
                Console.WriteLine("   Loading {0}...", filenameWithoutExtension);
                var image = new Bitmap(qualifiedFilename);

                resizingQueue.Add(new ImageContainer { Filename = filenameWithoutExtension, Image = image });
            });

            resizingQueue.CompleteAdding();
            Console.WriteLine("Completed loading task.");
        }
    }
}
