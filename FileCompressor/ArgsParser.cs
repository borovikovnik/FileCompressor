using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace FileCompressor
{
    static class ArgsParser
    {
        private const string HelpText = "Arguments:\n" +
                                        "-help || -h                         Release this text\n" +
                                        "-input [path] || -i [path]          Path to input file\n" +
                                        "-blocksize [size] || -bs [size]     Define block size in bytes(optional, 1MB by default)\n" +
                                        "-decompress || -dc                  Use, if you want to decompress file(optional)\n";

        public struct Arguments
        {
            public Arguments(string inputPath, int blockSize, bool isCompression = true)
            {
                InputPath = inputPath;
                BlockSize = blockSize;
                IsCompression = isCompression;             
            }

            internal string InputPath { get; set; }
            internal int BlockSize { get; set; }
            internal bool IsCompression { get; set; }          
        }

        public static Arguments Parse(string[] args)
        {
            const int mb = 1024*1024;
            var result = new Arguments("", mb);
            for (var i = 0; i < args.Length; i++)
            {
                try
                {
                    switch (args[i])
                    {
                        case "-help":
                        case "-h":
                            Console.WriteLine(HelpText);
                            Environment.Exit(0);
                            break;
                        case "-input":
                        case "-i":
                            result.InputPath = args[i + 1];
                            i++;
                            break;
                        case "-blocksize":
                        case "-bs":
                            result.BlockSize = Convert.ToInt32(args[i + 1]);
                            i++;
                            break;
                        case "-deccompress":
                        case "-dc":
                            result.IsCompression = false;
                            break;
                        default:
                            ErrorLogger.Log("Wrong Argument");
                            break;
                    }
                }                               
                catch (IndexOutOfRangeException)
                {
                    ErrorLogger.Log("Wrong number of arguments");
                }
                catch (Exception ex)
                { 
                    ErrorLogger.Log(ex.Message);
                }

                if (string.IsNullOrEmpty(result.InputPath))
                {
                    ErrorLogger.Log("No input file");
                }
            }
            return result;
        }

        
    }
}
