using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件管理器
    /// </summary>
    internal class PcapFileManager : IDisposable
    {
        private FileStream _PcapStream;
        private BinaryWriter _PcapWriter;

        public string FilePath { get; private set; }
        public long FileSize => _PcapStream?.Length ?? 0;
        public bool IsOpen => _PcapStream != null;

        public void Create(string filePath)
        {
            FilePath = filePath;
            _PcapStream = StreamHelper.CreateFileStream(
                filePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _PcapWriter = StreamHelper.CreateBinaryWriter(_PcapStream);
        }

        public void Open(string filePath)
        {
            FilePath = filePath;
            _PcapStream = StreamHelper.CreateFileStream(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _PcapWriter = StreamHelper.CreateBinaryWriter(_PcapStream);
        }

        public void WriteHeader(PcapFileHeader header)
        {
            _PcapWriter.Write(header.ToBytes());
        }

        public PcapFileHeader ReadHeader()
        {
            return StreamHelper.ReadStructure<PcapFileHeader>(_PcapStream);
        }

        public void WriteIndexEntry(PataFileIndexEntry entry)
        {
            _PcapWriter.Write(entry.ToBytes());
        }

        public async Task WriteIndexEntryAsync(
            byte[] indexBytes,
            CancellationToken cancellationToken
        )
        {
            await _PcapStream.WriteAsync(indexBytes, 0, indexBytes.Length, cancellationToken);
        }

        public void Flush()
        {
            _PcapWriter?.Flush();
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_PcapStream != null)
            {
                await _PcapStream.FlushAsync(cancellationToken);
            }
        }

        public void Close()
        {
            if (_PcapWriter != null)
            {
                _PcapWriter.Dispose();
                _PcapWriter = null;
            }

            if (_PcapStream != null)
            {
                _PcapStream.Dispose();
                _PcapStream = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
