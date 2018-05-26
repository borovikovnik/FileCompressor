using System.IO;


namespace FileCompressor
{
    class Program
    {
        static void Main(string[] args)
        {
            var arguments = ArgsParser.Parse(args);
            var c = new AsyncCompressor(arguments.BlockSize);
            c.Start(arguments.InputPath, arguments.IsCompression);
        }
    }
}
