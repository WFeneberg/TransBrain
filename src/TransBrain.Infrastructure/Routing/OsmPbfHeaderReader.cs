using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Routing;

/// <summary>
/// Reads the header block of an OpenStreetMap PBF file without any protobuf dependency — the
/// header is the first few hundred bytes and uses only a handful of fields. Geofabrik extracts
/// carry their region in the osmosis replication URL and the data cutoff in the replication
/// timestamp; merged or hand-built files usually only have the bounding box.
/// </summary>
public static class OsmPbfHeaderReader
{
    public sealed record OsmPbfHeader(
        MapExtractBoundingBox? BoundingBox,
        DateTimeOffset? ReplicationTimestamp,
        string? ReplicationBaseUrl);

    private const int MaxBlobHeaderSize = 64 * 1024;
    private const int MaxHeaderBlockSize = 4 * 1024 * 1024;

    /// <summary>Null when the stream is not a readable OSM PBF file; bad content never throws.</summary>
    public static async Task<OsmPbfHeader?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            // File layout: 4-byte big-endian BlobHeader length, BlobHeader, then the Blob it describes.
            byte[] lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes, cancellationToken);
            int blobHeaderLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
            if (blobHeaderLength <= 0 || blobHeaderLength > MaxBlobHeaderSize)
            {
                return null;
            }

            byte[] blobHeader = new byte[blobHeaderLength];
            await stream.ReadExactlyAsync(blobHeader, cancellationToken);
            (string? type, int dataSize) = ParseBlobHeader(blobHeader);
            if (type != "OSMHeader" || dataSize <= 0 || dataSize > MaxHeaderBlockSize)
            {
                return null;
            }

            byte[] blob = new byte[dataSize];
            await stream.ReadExactlyAsync(blob, cancellationToken);
            byte[]? headerBlock = DecodeBlob(blob);
            return headerBlock is null ? null : ParseHeaderBlock(headerBlock);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// The Geofabrik region path of a replication URL, e.g.
    /// "http://download.geofabrik.de/europe/germany/bayern/oberbayern-updates"
    /// → "europe/germany/bayern/oberbayern"; null when the URL carries no region.
    /// </summary>
    public static string? RegionFromReplicationUrl(string? replicationBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(replicationBaseUrl)
            || !Uri.TryCreate(replicationBaseUrl, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string path = uri.AbsolutePath.Trim('/');
        const string updatesSuffix = "-updates";
        if (path.EndsWith(updatesSuffix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^updatesSuffix.Length];
        }

        return path.Length == 0 ? null : path;
    }

    // BlobHeader: type = 1 (string), datasize = 3 (int32).
    private static (string? Type, int DataSize) ParseBlobHeader(ReadOnlySpan<byte> message)
    {
        string? type = null;
        int dataSize = 0;
        int position = 0;
        while (position < message.Length)
        {
            (int field, int wire) = ReadTag(message, ref position);
            if (field == 1 && wire == 2)
            {
                type = Encoding.UTF8.GetString(ReadLengthDelimited(message, ref position));
            }
            else if (field == 3 && wire == 0)
            {
                dataSize = checked((int)ReadVarint(message, ref position));
            }
            else
            {
                Skip(message, ref position, wire);
            }
        }

        return (type, dataSize);
    }

    // Blob: raw = 1 (bytes), raw_size = 2, zlib_data = 3 (bytes). Other compressions are not
    // produced by the tools this project documents and simply yield no header info.
    private static byte[]? DecodeBlob(ReadOnlySpan<byte> message)
    {
        int position = 0;
        while (position < message.Length)
        {
            (int field, int wire) = ReadTag(message, ref position);
            if (field == 1 && wire == 2)
            {
                return ReadLengthDelimited(message, ref position).ToArray();
            }

            if (field == 3 && wire == 2)
            {
                return InflateZlib(ReadLengthDelimited(message, ref position).ToArray());
            }

            Skip(message, ref position, wire);
        }

        return null;
    }

    private static byte[] InflateZlib(byte[] compressed)
    {
        using MemoryStream source = new(compressed);
        using ZLibStream inflate = new(source, CompressionMode.Decompress);
        using MemoryStream result = new();
        inflate.CopyTo(result);
        return result.ToArray();
    }

    // HeaderBlock: bbox = 1 (message), osmosis_replication_timestamp = 32 (int64 epoch seconds),
    // osmosis_replication_base_url = 34 (string).
    private static OsmPbfHeader ParseHeaderBlock(ReadOnlySpan<byte> message)
    {
        MapExtractBoundingBox? boundingBox = null;
        DateTimeOffset? timestamp = null;
        string? replicationUrl = null;
        int position = 0;
        while (position < message.Length)
        {
            (int field, int wire) = ReadTag(message, ref position);
            if (field == 1 && wire == 2)
            {
                boundingBox = ParseBoundingBox(ReadLengthDelimited(message, ref position));
            }
            else if (field == 32 && wire == 0)
            {
                long seconds = (long)ReadVarint(message, ref position);
                if (seconds > 0)
                {
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds);
                }
            }
            else if (field == 34 && wire == 2)
            {
                replicationUrl = Encoding.UTF8.GetString(ReadLengthDelimited(message, ref position));
            }
            else
            {
                Skip(message, ref position, wire);
            }
        }

        return new OsmPbfHeader(boundingBox, timestamp, replicationUrl);
    }

    // HeaderBBox: left = 1, right = 2, top = 3, bottom = 4 — all sint64 in nanodegrees.
    private static MapExtractBoundingBox? ParseBoundingBox(ReadOnlySpan<byte> message)
    {
        double? left = null, right = null, top = null, bottom = null;
        int position = 0;
        while (position < message.Length)
        {
            (int field, int wire) = ReadTag(message, ref position);
            if (wire == 0)
            {
                double degrees = DecodeZigZag(ReadVarint(message, ref position)) * 1e-9;
                switch (field)
                {
                    case 1: left = degrees; break;
                    case 2: right = degrees; break;
                    case 3: top = degrees; break;
                    case 4: bottom = degrees; break;
                }
            }
            else
            {
                Skip(message, ref position, wire);
            }
        }

        return left is { } west && right is { } east && top is { } north && bottom is { } south
            ? new MapExtractBoundingBox(
                MinLatitude: south, MinLongitude: west, MaxLatitude: north, MaxLongitude: east)
            : null;
    }

    private static (int Field, int Wire) ReadTag(ReadOnlySpan<byte> message, ref int position)
    {
        ulong tag = ReadVarint(message, ref position);
        return ((int)(tag >> 3), (int)(tag & 0x7));
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> message, ref int position)
    {
        ulong result = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            if (position >= message.Length)
            {
                throw new EndOfStreamException();
            }

            byte current = message[position++];
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw new EndOfStreamException();
    }

    private static ReadOnlySpan<byte> ReadLengthDelimited(ReadOnlySpan<byte> message, ref int position)
    {
        int length = checked((int)ReadVarint(message, ref position));
        if (length < 0 || position + length > message.Length)
        {
            throw new EndOfStreamException();
        }

        ReadOnlySpan<byte> value = message.Slice(position, length);
        position += length;
        return value;
    }

    private static void Skip(ReadOnlySpan<byte> message, ref int position, int wireType)
    {
        switch (wireType)
        {
            case 0:
                ReadVarint(message, ref position);
                break;
            case 1:
                position += 8;
                break;
            case 2:
                ReadLengthDelimited(message, ref position);
                break;
            case 5:
                position += 4;
                break;
            default:
                // Group wire types do not occur in PBF files; treat them as malformed input.
                throw new EndOfStreamException();
        }

        if (position > message.Length)
        {
            throw new EndOfStreamException();
        }
    }

    private static long DecodeZigZag(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);
}
