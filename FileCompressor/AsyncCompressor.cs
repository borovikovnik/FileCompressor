


using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace FileCompressor
{
    class AsyncCompressor
    {
        public AsyncCompressor(int blockSize)
        {
            _blockSize = blockSize;           
        }

        private const int InHeaderIndex = 4; // Index to unused bytes in header of compressed block. Will be used to save length of the block.
        private Stream _inputFile;
        private Stream _outputFile;
        private readonly int _blockSize;
        private byte[][] blocksData;
        private byte[][] compressedBlocksData;

        private void OpenStreams(string inputPath, bool isCompression)
        {
            _inputFile = new FileStream(inputPath, FileMode.Open);
            if (isCompression)
            {
                _outputFile = new FileStream(inputPath + ".gz", FileMode.OpenOrCreate);
            }
            else
            {
                string outputPath;
                var dir = Path.GetDirectoryName(inputPath);
                if (string.IsNullOrEmpty(dir))
                {
                    outputPath = Path.GetFileNameWithoutExtension(inputPath);
                }
                else
                {
                    var fileName = Path.GetFileNameWithoutExtension(inputPath);
                    outputPath = Path.Combine(dir, fileName);
                }
                _outputFile = new FileStream(outputPath, FileMode.OpenOrCreate);
            }
        }
        private void CloseStreams()
        {
            _inputFile.Close();
            _outputFile.Close();
        }
        public void Start(string inputPath, bool isCompression = true)
        {
            OpenStreams(inputPath, isCompression);
            var threadNumber = Environment.ProcessorCount;
            var threads = new Thread[threadNumber];
            blocksData = new byte[threadNumber][];
            compressedBlocksData = new byte[threadNumber][];

            if (isCompression)
            {
                Compress(threads);
            }
            else
            {
                Decompress(threads);
            }
            CloseStreams();
        }

        private void CompressBlock(object index)
        {
            var i = (int)index;
            using (var ms = new MemoryStream(blocksData[i].Length))
            {
                using (var gzs = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzs.Write(blocksData[i], 0, blocksData[i].Length);
                    
                }
                compressedBlocksData[i] = ms.ToArray();
            }
        }

        private void Compress(Thread[] threads)
        {
            try
            {
                while (_inputFile.Position < _inputFile.Length)
                {
                    var currNumberOfThreads = threads.Length;
                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        var currBlockSize = _blockSize;
                        if (_inputFile.Length - _inputFile.Position < currBlockSize)
                        {
                            currNumberOfThreads = i+1;
                            currBlockSize = (int)(_inputFile.Length - _inputFile.Position);
                        }
                        blocksData[i] = new byte[currBlockSize];
                        _inputFile.Read(blocksData[i], 0, currBlockSize);
                        threads[i] = new Thread(CompressBlock);
                        threads[i].Start(i);
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        threads[i].Join();
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        BitConverter.GetBytes(compressedBlocksData[i].Length)
                                        .CopyTo(compressedBlocksData[i], InHeaderIndex);
                        _outputFile.Write(compressedBlocksData[i], 0, compressedBlocksData[i].Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex.Message);
            }
        }


        public void DecompressBlock(object index)
        {
            var i = (int)index;
            using (var input = new MemoryStream(compressedBlocksData[i]))
            using (var ds = new GZipStream(input, CompressionMode.Decompress))
            {
                ds.Read(blocksData[i], 0, blocksData[i].Length);
            }
        }
        private void Decompress(Thread[] threads)
        {
            const int headerSize = 8;
            try
            {
                var buffer = new byte[headerSize];
                var currNumberOfThreads = threads.Length;

                while (_inputFile.Position < _inputFile.Length)
                {
                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        _inputFile.Read(buffer, 0, headerSize);
                        var compressedBlockLength = BitConverter.ToInt32(buffer, InHeaderIndex);
                        compressedBlocksData[i] = new byte[compressedBlockLength];
                        buffer.CopyTo(compressedBlocksData[i], 0);
                        _inputFile.Read(compressedBlocksData[i], headerSize, compressedBlockLength - headerSize);

                        if (_inputFile.Position >= _inputFile.Length)
                        {
                            currNumberOfThreads = i + 1;
                        }

                        var decompressedBlockLength = BitConverter.ToInt32(compressedBlocksData[i], compressedBlockLength - 4); // Length of decompressed block saved in last 4 bytes of compressed one.
                        blocksData[i] = new byte[decompressedBlockLength];

                        threads[i] = new Thread(DecompressBlock);
                        threads[i].Start(i);
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        threads[i].Join();
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        _outputFile.Write(blocksData[i], 0, blocksData[i].Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex.Message);
            }
        }

    }
}
