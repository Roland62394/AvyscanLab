using System;
using System.IO;

namespace AvyscanLab.Services;

public sealed class AviService : IAviService
{
    public bool IsAviFourCcKnownToFailWithAviSource(string path) =>
        TryGetAviVideoFourCc(path, out var fourCc)
        && string.Equals(fourCc, "HDYC", StringComparison.OrdinalIgnoreCase);

    public bool TryGetAviVideoFourCc(string filePath, out string fourCc)
    {
        fourCc = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 12) return false;
            if (new string(reader.ReadChars(4)) != "RIFF") return false;
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "AVI ") return false;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadUInt32();
                var dataStart = stream.Position;

                if (chunkId == "LIST" && chunkSize >= 4)
                {
                    var listType = new string(reader.ReadChars(4));
                    var listEnd = dataStart + chunkSize;
                    if (listType == "hdrl" && TryReadStreamHeaderFourCc(reader, stream, listEnd, out fourCc))
                        return true;
                    stream.Position = listEnd;
                }
                else
                {
                    stream.Position = dataStart + chunkSize;
                }

                if ((stream.Position & 1) == 1) stream.Position++;
            }
        }
        catch { }

        return false;
    }

    private static bool TryReadStreamHeaderFourCc(BinaryReader reader, Stream stream, long listEnd, out string fourCc)
    {
        fourCc = string.Empty;
        while (stream.Position + 8 <= listEnd)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            var dataStart = stream.Position;

            if (chunkId == "LIST" && chunkSize >= 4)
            {
                var listType = new string(reader.ReadChars(4));
                var nestedEnd = dataStart + chunkSize;

                if (listType == "strl")
                {
                    while (stream.Position + 8 <= nestedEnd)
                    {
                        var sub = new string(reader.ReadChars(4));
                        var subSize = reader.ReadUInt32();
                        var subStart = stream.Position;

                        if (sub == "strh" && subSize >= 8)
                        {
                            var fccType = new string(reader.ReadChars(4));
                            var fccHandler = new string(reader.ReadChars(4));
                            if (fccType == "vids") { fourCc = fccHandler; return true; }
                        }

                        stream.Position = subStart + subSize;
                        if ((stream.Position & 1) == 1) stream.Position++;
                    }
                }

                stream.Position = nestedEnd;
            }
            else
            {
                stream.Position = dataStart + chunkSize;
            }

            if ((stream.Position & 1) == 1) stream.Position++;
        }
        return false;
    }
}
