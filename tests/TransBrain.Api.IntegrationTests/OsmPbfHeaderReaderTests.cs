using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using TransBrain.Infrastructure.Routing;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// Pure unit tests (no containers): the PBF bytes are built by hand following the OSM PBF spec,
/// so the reader is checked against known field numbers and encodings rather than a fixture file.
/// </summary>
public class OsmPbfHeaderReaderTests
{
    private const string GeofabrikUrl = "http://download.geofabrik.de/europe/germany/bayern/oberbayern-updates";

    [Fact]
    public async Task ReadAsync_GeofabrikStyleZlibHeader_ParsesBboxTimestampAndUrl()
    {
        // Oberbayern-ish bbox in nanodegrees: left, right, top, bottom.
        byte[] headerBlock =
        [
            .. LengthDelimited(1,
            [
                .. VarintField(1, ZigZag(10_080_000_000)),
                .. VarintField(2, ZigZag(13_010_000_000)),
                .. VarintField(3, ZigZag(48_760_000_000)),
                .. VarintField(4, ZigZag(47_270_000_000)),
            ]),
            .. LengthDelimited(4, Encoding.UTF8.GetBytes("OsmSchema-V0.6")),
            .. VarintField(32, 1_762_630_800), // 2025-11-08T19:40:00Z
            .. LengthDelimited(34, Encoding.UTF8.GetBytes(GeofabrikUrl)),
        ];

        OsmPbfHeaderReader.OsmPbfHeader? header =
            await OsmPbfHeaderReader.ReadAsync(PbfFile(headerBlock, compress: true), CancellationToken.None);

        header.Should().NotBeNull();
        header!.ReplicationBaseUrl.Should().Be(GeofabrikUrl);
        header.ReplicationTimestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_762_630_800));
        header.BoundingBox.Should().NotBeNull();
        header.BoundingBox!.MinLongitude.Should().BeApproximately(10.08, 1e-6);
        header.BoundingBox.MaxLongitude.Should().BeApproximately(13.01, 1e-6);
        header.BoundingBox.MaxLatitude.Should().BeApproximately(48.76, 1e-6);
        header.BoundingBox.MinLatitude.Should().BeApproximately(47.27, 1e-6);
    }

    [Fact]
    public async Task ReadAsync_UncompressedHeaderWithoutReplicationInfo_StillYieldsTheBbox()
    {
        byte[] headerBlock =
        [
            .. LengthDelimited(1,
            [
                .. VarintField(1, ZigZag(-10_000_000_000)), // extracts west of Greenwich are negative
                .. VarintField(2, ZigZag(2_000_000_000)),
                .. VarintField(3, ZigZag(51_000_000_000)),
                .. VarintField(4, ZigZag(41_000_000_000)),
            ]),
        ];

        OsmPbfHeaderReader.OsmPbfHeader? header =
            await OsmPbfHeaderReader.ReadAsync(PbfFile(headerBlock, compress: false), CancellationToken.None);

        header.Should().NotBeNull();
        header!.ReplicationBaseUrl.Should().BeNull();
        header.ReplicationTimestamp.Should().BeNull();
        header.BoundingBox!.MinLongitude.Should().BeApproximately(-10.0, 1e-6);
        header.BoundingBox.MinLatitude.Should().BeApproximately(41.0, 1e-6);
    }

    [Fact]
    public async Task ReadAsync_WrongBlobType_AnswersNull()
    {
        byte[] block = LengthDelimited(34, Encoding.UTF8.GetBytes(GeofabrikUrl));

        OsmPbfHeaderReader.OsmPbfHeader? header = await OsmPbfHeaderReader.ReadAsync(
            PbfFile(block, compress: true, blobType: "OSMData"), CancellationToken.None);

        header.Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x00, 0x00 })] // truncated length prefix
    [InlineData(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0x0A })] // absurd BlobHeader length
    public async Task ReadAsync_NotAPbfFile_AnswersNullInsteadOfThrowing(byte[] bytes)
    {
        OsmPbfHeaderReader.OsmPbfHeader? header =
            await OsmPbfHeaderReader.ReadAsync(new MemoryStream(bytes), CancellationToken.None);

        header.Should().BeNull();
    }

    [Theory]
    [InlineData(GeofabrikUrl, "europe/germany/bayern/oberbayern")]
    [InlineData("https://download.geofabrik.de/europe/germany-updates/", "europe/germany")]
    [InlineData("http://example.org/custom/feed", "custom/feed")]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void RegionFromReplicationUrl_StripsHostAndUpdatesSuffix(string? url, string? expected)
        => OsmPbfHeaderReader.RegionFromReplicationUrl(url).Should().Be(expected);

    /// <summary>4-byte big-endian BlobHeader length, BlobHeader, then the Blob with the block.</summary>
    private static MemoryStream PbfFile(byte[] headerBlock, bool compress, string blobType = "OSMHeader")
    {
        byte[] blob = compress
            ?
            [
                .. VarintField(2, (ulong)headerBlock.Length), // raw_size
                .. LengthDelimited(3, Deflate(headerBlock)), // zlib_data
            ]
            : LengthDelimited(1, headerBlock); // raw

        byte[] blobHeader =
        [
            .. LengthDelimited(1, Encoding.UTF8.GetBytes(blobType)),
            .. VarintField(3, (ulong)blob.Length),
        ];

        byte[] lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, blobHeader.Length);
        return new MemoryStream([.. lengthPrefix, .. blobHeader, .. blob]);
    }

    private static byte[] Deflate(byte[] data)
    {
        using MemoryStream result = new();
        using (ZLibStream zlib = new(result, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return result.ToArray();
    }

    private static byte[] Varint(ulong value)
    {
        List<byte> bytes = [];
        do
        {
            byte current = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value == 0 ? current : (byte)(current | 0x80));
        }
        while (value != 0);
        return [.. bytes];
    }

    private static byte[] VarintField(int field, ulong value)
        => [.. Varint((ulong)(field << 3)), .. Varint(value)];

    private static byte[] LengthDelimited(int field, byte[] payload)
        => [.. Varint((ulong)(field << 3 | 2)), .. Varint((ulong)payload.Length), .. payload];

    private static ulong ZigZag(long value) => (ulong)((value << 1) ^ (value >> 63));
}
