using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace starpak;

internal class StringsFile
{
    private static readonly Encoding encoding = GetEncoding();

    public StringsFileType FileType { get; init; }

    private Dictionary<uint, string> _strings = new();

    private static Encoding GetEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, new EncoderExceptionFallback(), new DecoderExceptionFallback());
    }

    public StringsFile(StringsFileType type)
    {
        FileType = type;
    }

    public StringsFile(string inputPath, StringsFileType? type = null)
    {
        using var fs = File.OpenRead(inputPath);
        FileType = type ?? GetTypeFromPath(inputPath);
        ReadImpl(fs, FileType != StringsFileType.STRINGS);
    }

    private StringsFileType GetTypeFromPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension switch
        {
            ".strings" => StringsFileType.STRINGS,
            ".ilstrings" => StringsFileType.ILSTRINGS,
            ".dlstrings" => StringsFileType.DLSTRINGS,
            _ => throw new ArgumentException("Unknown file extension " + extension),
        };
    }

    private unsafe void ReadImpl(Stream s, bool hasLength)
    {
        var br = new BinaryReader(s, encoding, leaveOpen: true);

        int count = br.ReadInt32();
        int dataSize = br.ReadInt32();

        StringEntry[] entries = br.ReadStructArray<StringEntry>(count);

        byte[] data = br.ReadBytes(dataSize);

        fixed (byte* ptr = data)
        for (int i = 0; i < entries.Length; i++)
        {
            StringEntry entry = entries[i];
            int offset = entry.Offset;

            string text;
            if (hasLength)
            {
                int length = *(int*)(ptr + offset);
                text = encoding.GetString(ptr + offset + 4, length - 1);
            }
            else
            {
                int length = 0;
                while (ptr[offset + length] != 0) length++;
                text = encoding.GetString(ptr + offset, length);
            }

            _strings.Add(entry.StringID, text);
        }
    }

    public void Write(string outputPath)
    {
        using var fs = File.Create(outputPath);
        WriteImpl(fs, FileType != StringsFileType.STRINGS);
    }

    private unsafe void WriteImpl(Stream s, bool hasLength)
    {
        BinaryWriter bw = new(s, encoding, leaveOpen: true);

        bw.Write(_strings.Count);
        bw.BaseStream.Position += 4 + (8 * _strings.Count);

        long dataOffset = bw.BaseStream.Position;

        Dictionary<string, int> duplicateStringsOffset = new();

        StringEntry[] entries = new StringEntry[_strings.Count];
        byte[] buffer = new byte[16384];

        int i = 0;
        foreach (var kvpair in _strings)
        {
            uint stringID = kvpair.Key;
            string text = kvpair.Value;

            if (duplicateStringsOffset.TryGetValue(text, out int dupOffset))
            {
                entries[i++] = new StringEntry
                {
                    StringID = stringID,
                    Offset = dupOffset,
                };
                continue;
            }

            int offset = (int)(bw.BaseStream.Position - dataOffset);
            duplicateStringsOffset[text] = offset;

            entries[i++] = new StringEntry
            {
                StringID = stringID,
                Offset = offset,
            };

            int length = encoding.GetBytes(text, buffer);
            if (hasLength)
            {
                bw.Write(length + 1);
            }

            bw.Write(buffer, 0, length);
            bw.Write((byte)0);
        }

        int dataSize = (int)(bw.BaseStream.Position - dataOffset);
        bw.BaseStream.Position = 4;
        bw.Write(dataSize);
        
        bw.WriteStructArray(entries);
    }

    private record struct StringEntry
    {
        public uint StringID;
        public int Offset;
    }
}

public enum StringsFileType
{
    STRINGS,
    ILSTRINGS,
    DLSTRINGS,
}
