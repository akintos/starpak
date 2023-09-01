using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace starpak;

internal static class StreamExtensions
{
    public static unsafe T ReadStruct<T>(this BinaryReader br) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var buf = stackalloc byte[size];
        var span = new Span<byte>(buf, size);
        br.BaseStream.ReadExactly(span);
        return MemoryMarshal.Cast<byte, T>(span)[0];
    }

    public static T[] ReadStructArray<T>(this BinaryReader br, int count) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var result = new T[count];
        var buf = MemoryMarshal.Cast<T, byte>(result);
        br.BaseStream.ReadExactly(buf);
        return result;
    }

    public static void WriteStruct<T>(this BinaryWriter bw, T val) where T : struct
    {
        var span = MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref val, 1));
        bw.BaseStream.Write(span);
    }

    public static void WriteStructArray<T>(this BinaryWriter bw, T[] arr) where T : struct
    {
        var span = MemoryMarshal.Cast<T, byte>(arr);
        bw.BaseStream.Write(span);
    }

    private static readonly byte[] _buf = new byte[8192];

    public static void CopyToWithSize(this Stream i, Stream o, int size)
    {
        int sizeLeft = size;
        int read;
        while (sizeLeft > 0 && (read = i.Read(_buf, 0, Math.Min(8192, sizeLeft))) > 0)
        {
            o.Write(_buf, 0, read);
            sizeLeft -= read;
        }
    }
}
