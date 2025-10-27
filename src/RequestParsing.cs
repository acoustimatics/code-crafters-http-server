using System.Net.Sockets;
using System.Text;
using static Util;

record struct Span(int Start, int Length);

record RequestLine(
    string Method,
    string RequestTarget,
    string HttpVersion);

class Request(RequestLine requestLine, Dictionary<string, string> fieldLines, byte[] body)
{
    public RequestLine RequestLine { get; } = requestLine;

    public Dictionary<string, string> FieldLines { get; } = fieldLines;

    public byte[] Body { get; } = body;

    public string? GetFieldLineValue(string fieldName)
    {
        if (FieldLines.TryGetValue(fieldName, out var fieldValue))
        {
            return fieldValue;
        }

        return null;
    }
}

enum LWSTag { Normal, Continuation }

class RequestParser : IDisposable
{
    private const byte CR = 13;
    private const byte LF = 10;
    private const byte SP = 32;
    private const byte HT = 9;

    private readonly Socket socket;
    private readonly byte[] buffer = new byte[64];
    private int size = 0;
    private bool isMoreToRead = false;
    private int index = -1;
    private byte?[] octet = new byte?[3];

    public RequestParser(Socket socket)
    {
        this.socket = socket;
    }

    public async Task<Request> ParseRequest()
    {
        if (!isMoreToRead)
        {
            isMoreToRead = true;
            size = 0;
            index = -1;
            await Advance();
            await Advance();
            await Advance();
        }

        var requestLine = await RequestLine();
        var fieldLines = await FieldLines();
        var contentLenth = GetValueAsInt(fieldLines, "Content-Length") ?? 0;
        var body = await Body(contentLenth);
        return new Request(requestLine, fieldLines, body);
    }

    private async Task<RequestLine> RequestLine()
    {
        var method = new List<Byte>();
        while (octet[0] is byte o && o != SP)
        {
            method.Add(o);
            await Advance();
        }

        await Expect(SP);

        var requestURI = new List<byte>();
        while (octet[0] is byte o && o != SP)
        {
            requestURI.Add(o);
            await Advance();
        }

        await Expect(SP);

        var httpVersion = new List<byte>();
        while (octet[0] is byte o && (octet[0] != CR || octet[1] != LF))
        {
            httpVersion.Add(o);
            await Advance();
        }

        await Expect(CR);
        await Expect(LF);

        return new RequestLine(GetString(method), GetString(requestURI), GetString(httpVersion));
    }

    private async Task<Dictionary<string, string>> FieldLines()
    {
        // Using an explicit comparer b/c header names are case insensitive.
        var fieldLines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (octet[0] is byte && !IsCRLF(octet[0], octet[1]))
        {
            var fieldName = await ExpectToken();

            await Expect(':');

            var fieldValue = new List<byte>();
            while (octet[0] is byte o && !IsCRLF(octet[0], octet[1]))
            {
                fieldValue.Add(o);
                await Advance();
            }
            await Expect(CR);
            await Expect(LF);

            var fieldNameString = GetString(fieldName);
            var fieldValueString = GetString(fieldValue).Trim();

            fieldLines[fieldNameString] = fieldValueString;
        }

        await Expect(CR);
        await Expect(LF);

        return fieldLines;
    }

    private async Task<byte[]> Body(int contentLength)
    {
        var body = new byte[contentLength];
        for (var i = 0; i < contentLength && octet[0] is byte o; i++)
        {
            body[i] = o;
            await Advance();
        }
        return body;
    }

    private async Task Expect(byte o)
    {
        if (octet[0] == o)
        {
            await Advance();
        }
        else
        {
            throw new Exception($"unexpected octet {octet[0]}, expected {o}");
        }
    }

    private async Task Expect(char c)
    {
        await Expect((byte)c);
    }

    private async Task<List<byte>> ExpectToken()
    {
        if (octet[0] is byte o && IsToken(o))
        {
            return await Token();
        }
        else
        {
            throw new Exception($"expected token but found {octet[0]}");
        }
    }

    private async Task<List<byte>> Token()
    {
        var token = new List<byte>();
        while (octet[0] is byte o && IsToken(o))
        {
            token.Add(o);
            await Advance();
        }
        return token;
    }

    /// <summary>
    /// Advance once octet in the request.
    /// </summary>
    private async Task Advance()
    {
        index++;

        if (index >= size && isMoreToRead)
        {
            index = 0;
            size = await socket.ReceiveAsync(buffer);
            isMoreToRead = socket.Available > 0;
        }

        octet[0] = octet[1];
        octet[1] = octet[2];
        octet[2] = index < size ? buffer[index] : null;
    }

    /// <summary>
    /// Whether `o` can be in a TOKEN.
    /// </summary>
    private static bool IsToken(byte o)
    {
        return !IsCTL(o) && !IsSeparator(o);
    }

    /// <summary>
    /// Whether `o` is a control character (0 - 31) or DEL (127).
    /// </summary>
    private static bool IsCTL(byte? o)
    {
        return 0 <= o && o <= 31 || o == 127;
    }

    private static bool IsSeparator(byte? o)
    {
        return o == '(' || o == ')' || o == '<' || o == '>' || o == '@'
            || o == ',' || o == ';' || o == ':' || o == '\\' || o == '"'
            || o == '/' || o == '[' || o == ']' || o == '?' || o == '='
            || o == '{' || o == '}' || o == SP || o == HT;
    }

    private static bool IsCRLF(byte? o0, byte? o1)
    {
        return o0 == CR && o1 == LF;
    }

    private static string GetString(List<byte> octets)
    {
        var bytes = octets.ToArray();
        return Encoding.ASCII.GetString(bytes, 0, bytes.Length);
    }

    public void Dispose()
    {
        socket?.Dispose();
    }
}
