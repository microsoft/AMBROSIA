using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading;
using Ambrosia;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;

namespace Server
{
    [DataContract]
    public class Server : Immortal<IServerProxy>, IServer
    {
        
        [DataMember]
        internal int numberOfCalls = 0;
        [DataMember]
        internal double totalComputeTime = 0;
        public Server()
        {
        }

        protected override async Task<bool> OnFirstStart()
        {
            Console.WriteLine("*X* Server in Entry Point");
            return true;
        }

        public async Task<Tuple<byte[], long>> ResizeImageAsync(byte[] imageBytes, long sendTime)
        {
            long startMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Console.WriteLine("*X* sending delay: {0}ms", startMs - sendTime);

            int width = 200;
            int height = 200;
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);
            Image image;
            byte[] result;
            using (var ms = new MemoryStream(imageBytes))
            {
                image = Image.FromStream(ms);
            }

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            using (var ms = new MemoryStream()) 
            {
                destImage.Save(ms, ImageFormat.Png);
                result = ms.ToArray();
            }
            long endMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Console.WriteLine("*X* Compute time: {0}", endMs - startMs);
            return new Tuple<byte[], long>(result, endMs);
        }

        public async Task PrintComputeTimeAsync()
        {
            //Console.WriteLine("*X* Compute time: {0}", totalComputeTime / numberOfCalls);
            totalComputeTime = 0;
            numberOfCalls = 0;
        }
    }


    class ServerBootstrapper
    {
        private static int _receivePort = -1;
        private static int _sendPort = -1;
        private static string _perfServer;
        private static bool _autoContinue;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            // for debugging don't want to auto continue but for test automation want this to auto continue
            if (!_autoContinue)
            {
                Console.WriteLine("Pausing execution of " + _perfServer + ". Press enter to deploy and continue.");
                Console.ReadLine();
            }

            using (var c = AmbrosiaFactory.Deploy<IServer>(_perfServer, new Server(), _receivePort, _sendPort))
            {
                // nothing to call on c, just doing this for calling Dispose.
                Console.WriteLine("*X* Press enter to terminate program.");
                Thread.Sleep(3600 * 1000);
            }

        }

        private static void ParseAndValidateOptions(string[] args)
        {
            var options = ParseOptions(args, out var shouldShowHelp);
            ValidateOptions(options, shouldShowHelp);
        }

        private static OptionSet ParseOptions(string[] args, out bool shouldShowHelp)
        {
            var showHelp = false;
            var options = new OptionSet {
                { "s|serverName=", "The service name of the server [REQUIRED].", s => _perfServer = s },
                { "rp|receivePort=", "The service receive from port [REQUIRED].", rp => _receivePort = int.Parse(rp) },
                { "sp|sendPort=", "The service send to port. [REQUIRED]", sp => _sendPort = int.Parse(sp) },
                { "c|autoContinue", "Is continued automatically at start", c => _autoContinue = true },
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Invalid arguments: " + e.Message);
                ShowHelp(options);
                Environment.Exit(1);
            }

            shouldShowHelp = showHelp;

            return options;
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_perfServer == null) errorMessage += "Server name is required.\n";
            if (_sendPort == -1) errorMessage += "Send port is required.\n";
            if (_receivePort == -1) errorMessage += "Receive port is required.\n";

            if (errorMessage != string.Empty)
            {
                Console.WriteLine(errorMessage);
                ShowHelp(options);
                Environment.Exit(1);
            }

            if (shouldShowHelp) ShowHelp(options);
        }

        private static void ShowHelp(OptionSet options)
        {
            var name = typeof(ServerBootstrapper).Assembly.GetName().Name;
#if NETCORE
            Console.WriteLine($"Usage: dotnet {name}.dll [OPTIONS]\nOptions:");
#else
            Console.WriteLine($"Usage: {name}.exe [OPTIONS]\nOptions:");
#endif
            options.WriteOptionDescriptions(Console.Out);
            Environment.Exit(0);
        }
    }
}
