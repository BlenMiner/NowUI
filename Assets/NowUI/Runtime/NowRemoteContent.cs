using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace NowUI.Internal
{
    /// <summary>
    /// Buffers a response while refusing a chunk before it would cross the
    /// configured retained-byte limit. DownloadHandlerBuffer cannot provide
    /// that guarantee because its size can only be inspected after buffering.
    /// </summary>
    internal sealed class NowBoundedDownloadHandler : DownloadHandlerScript
    {
        const int SegmentSize = 64 * 1024;

        sealed class Segment
        {
            public readonly byte[] bytes;
            public int count;

            public Segment(byte[] bytes, int count = 0)
            {
                this.bytes = bytes;
                this.count = count;
            }
        }

        readonly long _byteLimit;
        readonly List<Segment> _segments = new List<Segment>(4);
        byte[] _declaredBuffer;
        int _declaredCount;
        long _receivedByteCount;
        byte[] _completedData;
        bool _completed;
        bool _limitExceeded;
        bool _storageFailed;
        bool _completedWithoutConsolidation;

        internal NowBoundedDownloadHandler(long byteLimit)
            : base(new byte[(int)Math.Max(1L, Math.Min(SegmentSize, Math.Max(0L, byteLimit)))])
        {
            _byteLimit = Math.Max(0L, byteLimit);
        }

        internal long byteLimit => _byteLimit;

        internal long receivedByteCount => _receivedByteCount;

        internal bool limitExceeded => _limitExceeded;

        internal bool completedWithoutConsolidation => _completedWithoutConsolidation;

        internal int segmentCount => _segments.Count;

        internal long retainedCapacityBytes
        {
            get
            {
                long total = _completedData?.LongLength ?? _declaredBuffer?.LongLength ?? 0L;

                for (int i = 0; i < _segments.Count; ++i)
                    total += _segments[i].bytes.LongLength;

                return total;
            }
        }

        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            if (contentLength > (ulong)_byteLimit)
            {
                _limitExceeded = true;
                return;
            }

            // A trustworthy length lets the completed payload hand off this exact
            // backing array instead of allocating a second full-size copy.
            if (_receivedByteCount == 0L &&
                _declaredBuffer == null &&
                _segments.Count == 0 &&
                contentLength <= int.MaxValue)
            {
                _declaredBuffer = contentLength > 0UL
                    ? new byte[(int)contentLength]
                    : Array.Empty<byte>();
            }
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (_completed || _limitExceeded || _storageFailed || data == null || dataLength <= 0)
                return false;

            if (dataLength > data.Length)
            {
                _storageFailed = true;
                return false;
            }

            if (dataLength > _byteLimit - _receivedByteCount)
            {
                _limitExceeded = true;
                return false;
            }

            long required = _receivedByteCount + dataLength;

            if (required > int.MaxValue)
            {
                _storageFailed = true;
                return false;
            }

            if (_declaredBuffer != null)
            {
                int available = _declaredBuffer.Length - _declaredCount;

                if (dataLength <= available)
                {
                    Buffer.BlockCopy(data, 0, _declaredBuffer, _declaredCount, dataLength);
                    _declaredCount += dataLength;
                    _receivedByteCount = required;
                    return true;
                }

                // A server can lie about Content-Length. Preserve the already
                // bounded bytes as the first segment and continue enforcing the
                // configured cap rather than silently writing past the array.
                _segments.Add(new Segment(_declaredBuffer, _declaredCount));
                _declaredBuffer = null;
                _declaredCount = 0;
            }

            AppendSegmented(data, dataLength);
            _receivedByteCount = required;
            return true;
        }

        protected override void CompleteContent()
        {
            _completed = true;

            if (_limitExceeded || _storageFailed)
                return;

            if (_declaredBuffer != null)
            {
                if (_declaredCount == _declaredBuffer.Length)
                {
                    _completedData = _declaredBuffer;
                    _completedWithoutConsolidation = true;
                }
                else
                {
                    _completedData = new byte[_declaredCount];
                    Buffer.BlockCopy(_declaredBuffer, 0, _completedData, 0, _declaredCount);
                }

                _declaredBuffer = null;
                _declaredCount = 0;
                return;
            }

            if (_segments.Count == 0)
            {
                _completedData = Array.Empty<byte>();
                _completedWithoutConsolidation = true;
                return;
            }

            if (_segments.Count == 1 &&
                _segments[0].count == _segments[0].bytes.Length &&
                _segments[0].count == _receivedByteCount)
            {
                _completedData = _segments[0].bytes;
                _completedWithoutConsolidation = true;
                _segments.Clear();
                return;
            }

            _completedData = new byte[(int)_receivedByteCount];
            int destinationOffset = 0;

            for (int i = 0; i < _segments.Count; ++i)
            {
                var segment = _segments[i];
                Buffer.BlockCopy(segment.bytes, 0, _completedData, destinationOffset, segment.count);
                destinationOffset += segment.count;
                _segments[i] = null;
            }

            _segments.Clear();
        }

        protected override byte[] GetData()
        {
            return GetBytes();
        }

        internal byte[] GetBytes()
        {
            if (_limitExceeded || _storageFailed)
                return null;

            if (_completedData != null)
                return _completedData;

            // Production reads only after CompleteContent. Keep this test/debug
            // path accurate without changing the segmented steady-state policy.
            if (_declaredBuffer != null)
            {
                var result = new byte[_declaredCount];
                Buffer.BlockCopy(_declaredBuffer, 0, result, 0, _declaredCount);
                return result;
            }

            var segmented = new byte[(int)_receivedByteCount];
            int destinationOffset = 0;

            for (int i = 0; i < _segments.Count; ++i)
            {
                var segment = _segments[i];
                Buffer.BlockCopy(segment.bytes, 0, segmented, destinationOffset, segment.count);
                destinationOffset += segment.count;
            }

            return segmented;
        }

        internal bool ReceiveDataForTesting(byte[] data, int dataLength)
        {
            return ReceiveData(data, dataLength);
        }

        internal void CompleteForTesting()
        {
            CompleteContent();
        }

        internal void ReceiveContentLengthForTesting(ulong contentLength)
        {
            ReceiveContentLengthHeader(contentLength);
        }

        public override void Dispose()
        {
            _declaredBuffer = null;
            _declaredCount = 0;
            _segments.Clear();
            _completedData = null;
            base.Dispose();
        }

        void AppendSegmented(byte[] data, int dataLength)
        {
            int sourceOffset = 0;

            while (sourceOffset < dataLength)
            {
                Segment tail = _segments.Count > 0 ? _segments[_segments.Count - 1] : null;

                if (tail == null || tail.count == tail.bytes.Length)
                {
                    long remainingBudget = _byteLimit -
                        (_receivedByteCount + sourceOffset);
                    int capacity = (int)Math.Min(SegmentSize, remainingBudget);
                    tail = new Segment(new byte[capacity]);
                    _segments.Add(tail);
                }

                int copyCount = Math.Min(dataLength - sourceOffset, tail.bytes.Length - tail.count);
                Buffer.BlockCopy(data, sourceOffset, tail.bytes, tail.count, copyCount);
                tail.count += copyCount;
                sourceOffset += copyCount;
            }
        }
    }

    /// <summary>
    /// Reads only the fixed ZIP end records needed to enforce an entry-count
    /// limit before ZipArchive materializes its central-directory entry list.
    /// </summary>
    internal static class NowZipArchivePreflight
    {
        const uint EndOfCentralDirectorySignature = 0x06054b50u;
        const uint Zip64EndOfCentralDirectorySignature = 0x06064b50u;
        const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50u;
        const int EndOfCentralDirectoryLength = 22;
        const int Zip64EndOfCentralDirectoryMinimumLength = 56;
        const int Zip64LocatorLength = 20;

        internal static ulong Validate(byte[] bytes, int configuredEntryLimit)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            int entryLimit = Math.Max(1, configuredEntryLimit);
            int endOffset = FindEndOfCentralDirectory(bytes);
            ushort diskNumber = ReadUInt16(bytes, endOffset + 4);
            ushort centralDirectoryDisk = ReadUInt16(bytes, endOffset + 6);
            ushort entriesOnDisk16 = ReadUInt16(bytes, endOffset + 8);
            ushort totalEntries16 = ReadUInt16(bytes, endOffset + 10);
            uint centralDirectorySize32 = ReadUInt32(bytes, endOffset + 12);
            uint centralDirectoryOffset32 = ReadUInt32(bytes, endOffset + 16);

            if (diskNumber != 0 || centralDirectoryDisk != 0)
                throw InvalidArchive("multi-disk ZIP archives are not supported");

            bool usesZip64 = entriesOnDisk16 == ushort.MaxValue ||
                totalEntries16 == ushort.MaxValue ||
                centralDirectorySize32 == uint.MaxValue ||
                centralDirectoryOffset32 == uint.MaxValue;

            if (!usesZip64)
            {
                if (entriesOnDisk16 != totalEntries16)
                    throw InvalidArchive("central-directory entry counts do not match");

                EnforceEntryLimit(totalEntries16, entryLimit);
                ValidateCentralDirectory(
                    bytes,
                    centralDirectoryOffset32,
                    centralDirectorySize32,
                    (ulong)endOffset,
                    totalEntries16,
                    entryLimit);
                return totalEntries16;
            }

            if (endOffset < Zip64LocatorLength)
                throw InvalidArchive("the ZIP64 end-of-central-directory locator is missing or truncated");

            int locatorOffset = endOffset - Zip64LocatorLength;

            if (ReadUInt32(bytes, locatorOffset) != Zip64EndOfCentralDirectoryLocatorSignature)
                throw InvalidArchive("the ZIP64 end-of-central-directory locator is missing");

            uint recordDisk = ReadUInt32(bytes, locatorOffset + 4);
            ulong recordOffset = ReadUInt64(bytes, locatorOffset + 8);
            uint totalDisks = ReadUInt32(bytes, locatorOffset + 16);

            if (recordDisk != 0 || totalDisks != 1)
                throw InvalidArchive("multi-disk ZIP64 archives are not supported");

            if (recordOffset > int.MaxValue ||
                recordOffset + Zip64EndOfCentralDirectoryMinimumLength > (ulong)locatorOffset)
            {
                throw InvalidArchive("the ZIP64 end-of-central-directory record is outside the archive");
            }

            int zip64Offset = (int)recordOffset;

            if (ReadUInt32(bytes, zip64Offset) != Zip64EndOfCentralDirectorySignature)
                throw InvalidArchive("the ZIP64 end-of-central-directory record is missing");

            ulong recordBodySize = ReadUInt64(bytes, zip64Offset + 4);

            if (recordBodySize < 44UL ||
                recordBodySize > (ulong)locatorOffset - recordOffset - 12UL)
            {
                throw InvalidArchive("the ZIP64 end-of-central-directory record is truncated");
            }

            uint zip64DiskNumber = ReadUInt32(bytes, zip64Offset + 16);
            uint zip64CentralDirectoryDisk = ReadUInt32(bytes, zip64Offset + 20);
            ulong entriesOnDisk64 = ReadUInt64(bytes, zip64Offset + 24);
            ulong totalEntries64 = ReadUInt64(bytes, zip64Offset + 32);
            ulong centralDirectorySize64 = ReadUInt64(bytes, zip64Offset + 40);
            ulong centralDirectoryOffset64 = ReadUInt64(bytes, zip64Offset + 48);

            if (zip64DiskNumber != 0 || zip64CentralDirectoryDisk != 0)
                throw InvalidArchive("multi-disk ZIP64 archives are not supported");

            if (entriesOnDisk64 != totalEntries64)
                throw InvalidArchive("ZIP64 central-directory entry counts do not match");

            EnforceEntryLimit(totalEntries64, entryLimit);
            ValidateCentralDirectory(
                bytes,
                centralDirectoryOffset64,
                centralDirectorySize64,
                recordOffset,
                totalEntries64,
                entryLimit);
            return totalEntries64;
        }

        static int FindEndOfCentralDirectory(byte[] bytes)
        {
            if (bytes.Length < EndOfCentralDirectoryLength)
                throw InvalidArchive("the end-of-central-directory record is truncated");

            int firstPossibleOffset = Math.Max(
                0,
                bytes.Length - EndOfCentralDirectoryLength - ushort.MaxValue);

            for (int offset = bytes.Length - EndOfCentralDirectoryLength;
                offset >= firstPossibleOffset;
                --offset)
            {
                if (ReadUInt32(bytes, offset) != EndOfCentralDirectorySignature)
                    continue;

                ushort commentLength = ReadUInt16(bytes, offset + 20);

                if (offset + EndOfCentralDirectoryLength + commentLength == bytes.Length)
                    return offset;
            }

            throw InvalidArchive("the end-of-central-directory record is missing or malformed");
        }

        static void EnforceEntryLimit(ulong entryCount, int entryLimit)
        {
            if (entryCount > (ulong)entryLimit)
            {
                throw new FormatException(
                    $"dotLottie archive has {entryCount} entries; the configured limit is {entryLimit}.");
            }
        }

        static void ValidateCentralDirectory(
            byte[] bytes,
            ulong offset,
            ulong size,
            ulong endRecordOffset,
            ulong declaredEntries,
            int entryLimit)
        {
            if (offset > int.MaxValue || size > int.MaxValue ||
                offset > endRecordOffset || size != endRecordOffset - offset)
            {
                throw InvalidArchive("the central directory is outside the archive");
            }

            int position = (int)offset;
            int end = checked(position + (int)size);
            ulong actualEntries = 0UL;

            while (position < end)
            {
                EnsureAvailable(bytes, position, sizeof(uint));
                uint signature = ReadUInt32(bytes, position);

                if (signature == 0x02014b50u)
                {
                    const int fixedHeaderLength = 46;
                    EnsureAvailable(bytes, position, fixedHeaderLength);
                    int variableLength = ReadUInt16(bytes, position + 28) +
                        ReadUInt16(bytes, position + 30) +
                        ReadUInt16(bytes, position + 32);
                    int next = checked(position + fixedHeaderLength + variableLength);

                    if (next > end)
                        throw InvalidArchive("a central-directory entry is truncated");

                    ++actualEntries;
                    EnforceEntryLimit(actualEntries, entryLimit);
                    position = next;
                    continue;
                }

                // The optional central-directory digital-signature record is
                // not an entry and, when present, must finish the directory.
                if (signature == 0x05054b50u)
                {
                    const int fixedSignatureLength = 6;
                    EnsureAvailable(bytes, position, fixedSignatureLength);
                    int next = checked(
                        position + fixedSignatureLength + ReadUInt16(bytes, position + 4));

                    if (next != end)
                        throw InvalidArchive("the central-directory digital signature is malformed");

                    position = next;
                    continue;
                }

                throw InvalidArchive("the central directory contains an invalid record");
            }

            if (actualEntries != declaredEntries)
                throw InvalidArchive("the declared and actual central-directory entry counts do not match");
        }

        static ushort ReadUInt16(byte[] bytes, int offset)
        {
            EnsureAvailable(bytes, offset, sizeof(ushort));
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        static uint ReadUInt32(byte[] bytes, int offset)
        {
            EnsureAvailable(bytes, offset, sizeof(uint));
            return (uint)(bytes[offset] |
                bytes[offset + 1] << 8 |
                bytes[offset + 2] << 16 |
                bytes[offset + 3] << 24);
        }

        static ulong ReadUInt64(byte[] bytes, int offset)
        {
            EnsureAvailable(bytes, offset, sizeof(ulong));
            return ReadUInt32(bytes, offset) |
                (ulong)ReadUInt32(bytes, offset + sizeof(uint)) << 32;
        }

        static void EnsureAvailable(byte[] bytes, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > bytes.Length - count)
                throw InvalidArchive("a ZIP end record is truncated");
        }

        static FormatException InvalidArchive(string reason)
        {
            return new FormatException($"Invalid dotLottie archive: {reason}.");
        }
    }
}
