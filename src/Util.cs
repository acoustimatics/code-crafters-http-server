using System.IO.Compression;
using System.Text;

static class Util
{
    public static IEnumerable<string> ParseCommaSeparatedList(string text)
    {
        var item = new StringBuilder();

        foreach (var c in text)
        {
            if (c == ',')
            {
                yield return item.ToString();
                item.Clear();
            }
            else
            {
                item.Append(c);
            }
        }

        yield return item.ToString();
    }

    public static async Task<byte[]> GzipCompress(byte[] inBytes)
    {
        using var outStream = new MemoryStream();

        // Dispose `gzipStream` before creating array to make sure the write
        // all data is flushed.
        using (var gzipStream = new GZipStream(outStream, CompressionMode.Compress))
        {
            await gzipStream.WriteAsync(inBytes, 0, inBytes.Length);
        }

        return outStream.ToArray();
    }

    public static int? GetValueAsInt(Dictionary<string, string> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }
        }

        return null;
    }
}