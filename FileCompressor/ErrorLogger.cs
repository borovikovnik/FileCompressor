using System;
using System.Diagnostics;

namespace FileCompressor
{
    class ErrorLogger
    {
        public static void Log(string errorMsg)
        {
            Console.WriteLine(errorMsg);
            Trace.TraceError(errorMsg);
            Environment.Exit(1);
        }
    }
}
