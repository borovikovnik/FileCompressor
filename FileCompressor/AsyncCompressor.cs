using System;
using System.IO;
using System.IO.Compression;
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
        private byte[][] _blocksData;
        private byte[][] _compressedBlocksData;

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
            _blocksData = new byte[threadNumber][];
            _compressedBlocksData = new byte[threadNumber][];

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
            using (var ms = new MemoryStream(_blocksData[i].Length))
            {
                using (var gzs = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzs.Write(_blocksData[i], 0, _blocksData[i].Length);
                    
                }
                _compressedBlocksData[i] = ms.ToArray();
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
                        _blocksData[i] = new byte[currBlockSize];
                        _inputFile.Read(_blocksData[i], 0, currBlockSize);
                        threads[i] = new Thread(CompressBlock);
                        threads[i].Start(i);
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        threads[i].Join();
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        BitConverter.GetBytes(_compressedBlocksData[i].Length)
                                        .CopyTo(_compressedBlocksData[i], InHeaderIndex);
                        _outputFile.Write(_compressedBlocksData[i], 0, _compressedBlocksData[i].Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex.Message);
            }
        }


        private void DecompressBlock(object index)
        {
            var i = (int)index;
            using (var input = new MemoryStream(_compressedBlocksData[i]))
            using (var ds = new GZipStream(input, CompressionMode.Decompress))
            {
                ds.Read(_blocksData[i], 0, _blocksData[i].Length);
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
                        _compressedBlocksData[i] = new byte[compressedBlockLength];
                        buffer.CopyTo(_compressedBlocksData[i], 0);
                        _inputFile.Read(_compressedBlocksData[i], headerSize, compressedBlockLength - headerSize);

                        if (_inputFile.Position >= _inputFile.Length)
                        {
                            currNumberOfThreads = i + 1;
                        }

                        var decompressedBlockLength = BitConverter.ToInt32(_compressedBlocksData[i], compressedBlockLength - 4); // Length of decompressed block saved in last 4 bytes of compressed one.
                        _blocksData[i] = new byte[decompressedBlockLength];

                        threads[i] = new Thread(DecompressBlock);
                        threads[i].Start(i);
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        threads[i].Join();
                    }

                    for (var i = 0; i < currNumberOfThreads; i++)
                    {
                        _outputFile.Write(_blocksData[i], 0, _blocksData[i].Length);
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
