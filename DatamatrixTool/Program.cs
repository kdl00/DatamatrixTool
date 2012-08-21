using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using CommandLine;
using CommandLine.Text;

using DataMatrix.net;

namespace ConsoleApplication3
{
    class Program
    {
        class Options : CommandLineOptionsBase
        {
            [Option("r", "read", DefaultValue=false, HelpText = "Sets mode to read an existing datamatrix image file")]
            public bool readMode { get; set; }

            [Option("w", "write", DefaultValue = false, HelpText = "Sets mode to write (or overwrite) a datamatrix image file")]
            public bool writeMode { get; set; }

            [Option("f", "in-file", DefaultValue=null, HelpText = "In write mode: FILE containing text to be used in the datamatrix\nIn read mode: FILE containing a datamatrix image to decode")]
            public string inFile { get; set; }

            [Option("t", "text", DefaultValue=null, HelpText="Text to be represented in the datamatrix out-file")]
            public string text { get; set; }

            [Option("o", "out-file", DefaultValue = null, HelpText = "In write mode: FILE generated containing the datamatrix image (supported image formats: *.png, *.gif, *.bmp, *.jpeg, *.jpg, *.tiff)\nIn read mode: the decoded message will be written to FILE")]
            public string outFile { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                var help = new HelpText
                {
                    Heading = new HeadingInfo(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name.ToString(),
                       System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()),
                    Copyright = new CopyrightInfo(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name.ToString(), 2012),
                    AdditionalNewLineAfterOption = false,
                    AddDashesToOption = true
                };
                help.AddOptions(this);
                return help;
            }
        }

        static void Main(string[] args)
        {
            var options = new Options();
            var parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            try
            {
                if (!parser.ParseArguments(args, options))
                {
                    Console.Read();
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                errorPrompt("When parsing command line arguments an exception occurred:\n{0}", ex.Message);
            }

            if (options.writeMode)
            {
                writeDataMatrix(options);
                Console.Read();
                Environment.Exit(0);
            }

            if (options.readMode)
            {
                readDataMatrix(options);
                Console.Read();
                Environment.Exit(0);
            }
        }

        static void readDataMatrix(Options options)
        {
            if (!File.Exists(options.inFile))
            {
                Console.WriteLine("The file to be read '{0}' does not exist", options.inFile);
                Console.Read();
                Environment.Exit(0);
            }

            DmtxImageDecoder dec = new DmtxImageDecoder();
            Bitmap bmp = new Bitmap(options.inFile);
            List<string> decodedData = new List<string>();
            // since the decoder may take quite a bit of time to decode 
            // ensure we'll timeout instead of having the user wait forever
            decodedData = dec.DecodeImage(bmp, 1, TimeSpan.MaxValue);
            if (decodedData.Count == 0)
            {
                Console.WriteLine("Message could not be decoded");
                Console.Read();
                Environment.Exit(0);
            }

            Console.WriteLine("Message found:");
            foreach (string line in decodedData)
                Console.Write(line);

            if (!string.IsNullOrWhiteSpace(options.outFile))
            {
                try
                {
                    StreamWriter writer = new StreamWriter(options.outFile, false);
                    foreach (string line in decodedData)
                        writer.Write(line);
                    writer.Close();
                }
                catch (Exception ex)
                {
                    errorPrompt("When trying to write decoded message to file '{1}' an exception occurred:\n{1}", options.outFile, ex.Message);
                }
                Console.WriteLine("\nMessage contents are in {0}", options.outFile);
            }
        }

        static void writeDataMatrix(Options options)
        {
            // can only use text or file, not both
            if ((string.IsNullOrWhiteSpace(options.inFile) && string.IsNullOrWhiteSpace(options.text))
                || (!string.IsNullOrWhiteSpace(options.inFile) && !string.IsNullOrWhiteSpace(options.text)))
            {
                errorPrompt("You must specify either the in-file or text switch when writing a datamatrix image however you cannot use both switches");
            }

            // do we have an out-file in a supported image format?
            if (!(options.outFile.Trim().ToLower().EndsWith(".png") || options.outFile.Trim().ToLower().EndsWith(".bmp") || options.outFile.Trim().ToLower().EndsWith(".gif") || options.outFile.Trim().ToLower().EndsWith(".jpg") || options.outFile.Trim().ToLower().EndsWith(".jpeg") || options.outFile.Trim().ToLower().EndsWith(".tiff")))
            {
                errorPrompt("Out-file was not in a supported format");
            }

            // what is to be encoded into the datamatrix
            string data = null;

            if (!string.IsNullOrWhiteSpace(options.inFile))
            {
                if (!File.Exists(options.inFile))
                {
                    errorPrompt("The in-file '{0}' does not exist", options.inFile);
                }

                FileInfo fi = new FileInfo(options.inFile);
                if (fi.Length > 1000)
                {
                    Console.WriteLine("You are trying to encode a large file this may take some time... continue? (y/[n])");
                    char ret = Console.ReadKey(true).KeyChar;
                    if (Char.ToLower(ret) != 'y')
                        Environment.Exit(0);
                }

                try
                {
                    StreamReader reader = new StreamReader(options.inFile);
                    data = reader.ReadToEnd();
                    reader.Close();
                }
                catch (Exception ex)
                {
                    errorPrompt("When reading file '{0}' an exception occurred:\n{1}", options.inFile, ex.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.text))
                data = options.text;

            if (string.IsNullOrWhiteSpace(data))
            {
                errorPrompt("No data to encode, aborting...");
            }

            DmtxImageEncoderOptions opts = new DmtxImageEncoderOptions();
            opts.ForeColor = Color.Black;
            opts.BackColor = Color.White;
            opts.ModuleSize = 8;
            opts.MarginSize = 60;
            opts.Scheme = DmtxScheme.DmtxSchemeAsciiGS1;

            Console.WriteLine("Encoding data...");
            DmtxImageEncoder enc = new DmtxImageEncoder();

            Bitmap encodedBitmap = enc.EncodeImage(data, opts);

            try
            {
                // set the image format according to outFile's file extension; default to png 
                string imageFormat = options.outFile.Substring(options.outFile.LastIndexOf('.') + 1, options.outFile.Length - options.outFile.LastIndexOf('.') - 1);
                switch (imageFormat)
                {
                    case "gif":
                        encodedBitmap.Save(options.outFile, ImageFormat.Gif);
                        break;
                    case "bmp":
                        encodedBitmap.Save(options.outFile, ImageFormat.Bmp);
                        break;
                    case "jpg":
                    case "jpeg":
                        encodedBitmap.Save(options.outFile, ImageFormat.Jpeg);
                        break;
                    case "tiff":
                        encodedBitmap.Save(options.outFile, ImageFormat.Tiff);
                        break;
                    case "png":
                        encodedBitmap.Save(options.outFile, ImageFormat.Png);
                        break;
                    default:
                        // this case should not happen due to prior options.outfile.endswith() checks

                        // we want the file extension to be correct if an unsupported image format
                        // is given by the user
                        options.outFile = options.outFile + ".png";
                        encodedBitmap.Save(options.outFile, ImageFormat.Png);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("When saving the datamatrix image an exception occurred:\n{0}", ex.Message);
            }
        }

        static void errorPrompt(string msg, params object[] args)
        {
            Console.WriteLine(msg, args);
            Console.WriteLine("Press any key to continue . . .");
            Console.Read();
            Environment.Exit(-1);
        }
    }
}
