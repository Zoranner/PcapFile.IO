using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PATA文件管理器
    /// </summary>
    internal class PataFileManager : IDisposable
    {
        private FileStream _PataStream;
        private BinaryWriter _PataWriter;

        public string FilePath { get; private set; }
        public long Position => _PataStream?.Position ?? 0;
        public long FileSize => _PataStream?.Length ?? 0;
        public bool IsOpen => _PataStream != null;

        public void Create(string pcapFilePath)
        {
            var directory = Path.GetDirectoryName(pcapFilePath);
            var pataDirectory = Path.Combine(directory, "Packet_Data");
            Directory.CreateDirectory(pataDirectory);

            FilePath = Path.Combine(pataDirectory, "data_001.pata");
            _PataStream = StreamHelper.CreateFileStream(
                FilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _PataWriter = StreamHelper.CreateBinaryWriter(_PataStream);
        }

        public void Open(string pcapFilePath)
        {
            var directory = Path.GetDirectoryName(pcapFilePath);
            var pataDirectory = Path.Combine(directory, "Packet_Data");
            FilePath = Path.Combine(pataDirectory, "data_001.pata");

            _PataStream = StreamHelper.CreateFileStream(
                FilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _PataWriter = StreamHelper.CreateBinaryWriter(_PataStream);
        }

        public void WriteHeader(PataFileHeader header)
        {
            _PataWriter.Write(header.ToBytes());
        }

        public void WritePacket(DataPacket packet)
        {
            _PataWriter.Write(packet.Header.ToBytes());
            _PataWriter.Write(packet.Data);
        }

        public async Task WritePacketAsync(DataPacket packet, CancellationToken cancellationToken)
        {
            var headerBytes = packet.Header.ToBytes();
            await _PataStream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken);
            await _PataStream.WriteAsync(packet.Data, 0, packet.Data.Length, cancellationToken);
        }

        public void Flush()
        {
            _PataWriter?.Flush();
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_PataStream != null)
            {
                await _PataStream.FlushAsync(cancellationToken);
            }
        }

        public void Close()
        {
            if (_PataWriter != null)
            {
                _PataWriter.Dispose();
                _PataWriter = null;
            }

            if (_PataStream != null)
            {
                _PataStream.Dispose();
                _PataStream = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
