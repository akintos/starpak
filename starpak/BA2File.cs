using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace starpak;

internal class BA2File : IDisposable
{
    private const uint BA2_SIGNATURE = 0x58445442;

    private readonly Stream _stream;

    private BA2Header Header { get; set; }

    private GeneralBlock[] GeneralBlocks { get; set; }
    private string[] FileNameArray { get; set; }

    private Dictionary<string, byte[]> _fileReplaceDict = new();

    public BA2File(string path)
    {
        _stream = File.OpenRead(path);
        BinaryReader br = new BinaryReader(_stream);

        Header = br.ReadStruct<BA2Header>();

        if (Header.Signature != BA2_SIGNATURE)
        {
            throw new IOException("Invalid BA2 signature");
        }
        
        if (Header.Version == 2)
        {
            br.BaseStream.Position += 8;
        }

        if (Header.ArchiveType == BA2ArchiveType.GNRL)
        {
            GeneralBlocks = br.ReadStructArray<GeneralBlock>(Header.FileCount);
        }
        else
        {
            throw new NotImplementedException("Only GNRL archives are supported");
        }

        br.BaseStream.Position = Header.NamesOffset;

        byte[] nameBuffer = new byte[0xFF];

        FileNameArray = new string[Header.FileCount];
        for (var i = 0; i < Header.FileCount; i++)
        {
            int nameLength = br.ReadUInt16();
            br.BaseStream.Read(nameBuffer, 0, nameLength);
            FileNameArray[i] = Encoding.UTF8.GetString(nameBuffer, 0, nameLength);
        }
    }

    public void ReplaceFile(string name, byte[] data)
    {
        _fileReplaceDict[name] = data;
    }

    public void Write(string path)
    {
        using var fs = File.Create(path);
        WriteImpl(fs);
    }

    public void WriteImpl(Stream s)
    {
        BinaryWriter bw = new(s);
        int files = GeneralBlocks.Length;

        BA2Header newHeader = Header with { NamesOffset = 0 };
        long dataPos = Marshal.SizeOf<BA2Header>() + (Marshal.SizeOf<GeneralBlock>() * GeneralBlocks.Length) + 8;
        bw.BaseStream.Position = dataPos;

        var newGeneralBlocks = new GeneralBlock[files];
        var sourceSpan = MemoryMarshal.Cast<GeneralBlock, byte>(GeneralBlocks);
        var targetSpan = MemoryMarshal.Cast<GeneralBlock, byte>(newGeneralBlocks);
        sourceSpan.CopyTo(targetSpan);

        for (int i = 0; i < files; i++)
        {
            newGeneralBlocks[i].Offset = s.Position;

            if (_fileReplaceDict.TryGetValue(FileNameArray[i], out byte[]? replaceFile) && replaceFile is not null)
            {
                newGeneralBlocks[i].UnpackedSize = replaceFile.Length;
                s.Write(replaceFile);
            }
            else
            {
                GeneralBlock originalBlock = GeneralBlocks[i];
                _stream.Position = originalBlock.Offset;
                int size = originalBlock.PackedSize == 0 ? originalBlock.UnpackedSize : throw new NotImplementedException("Compressed file is not supported");
                _stream.CopyToWithSize(s, size);
            }
        }

        newHeader.NamesOffset = s.Position;
        
        // write names
        foreach (var name in FileNameArray)
        {
            bw.Write((ushort)name.Length);
            bw.Write(Encoding.UTF8.GetBytes(name));
        }

        // write header
        bw.BaseStream.Position = 0;
        bw.WriteStruct(newHeader);
        bw.Write((ulong)1);

        // write general blocks
        bw.WriteStructArray(newGeneralBlocks);
    }

    public void ExtractAll(string outputDirectory)
    {
        for (var i = 0; i < Header.FileCount; i++)
        {
            string outputPath = Path.Combine(outputDirectory, FileNameArray[i]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ExtractFile(i, outputPath);
        }
    }

    public void ExtractFile(int i, string outputPath)
    {
        var block = GeneralBlocks[i];

        using var fs = File.Create(outputPath);
        _stream.Position = block.Offset;
        var buf = new byte[8192];
        int size = block.PackedSize == 0 ? GeneralBlocks[i].UnpackedSize : throw new NotImplementedException("Compressed file is not supported");
        _stream.CopyToWithSize(fs, size);
    }

    private struct BA2Header
    {
        public uint Signature;
        public uint Version;
        public BA2ArchiveType ArchiveType;
        public int FileCount;
        public long NamesOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct GeneralBlock
    {
        public uint NameHash;
        private fixed byte _extension[4];
        public uint DirectoryHash;
        public uint Flags;
        public long Offset;
        public int PackedSize;
        public int UnpackedSize;
        public uint Padding; // always 0xBAADFOOD

        public string Extension
        {
            get
            {
                fixed (byte* ptr = _extension)
                {
                    return Encoding.ASCII.GetString(ptr, 4);
                }
            }

            set
            {
                if (value.Length > 4)
                {
                    throw new ArgumentException("Extension must be 4 characters or less");
                }

                fixed (byte* ptr = _extension)
                {
                    var span = new Span<byte>(ptr, 4);
                    span.Fill(0);
                    Encoding.ASCII.GetBytes(value, span);
                }
            }
        }
    }

    private enum BA2ArchiveType
    {
        GNRL = 0x4C524E47,
    }

    public void Dispose()
    {
        ((IDisposable)_stream).Dispose();
    }
}
