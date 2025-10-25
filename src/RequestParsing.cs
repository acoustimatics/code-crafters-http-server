using System.Text;

record struct Span(int Start, int Length);

record RequestLine(
    string Method,
    string RequestTarget,
    string HttpVersion);

record FieldLine(
    string FieldName,
    string FieldValue);

class Request(RequestLine requestLine, List<FieldLine> fieldLines, byte[] body)
{
    public RequestLine RequestLine { get; } = requestLine;

    public List<FieldLine> FieldLines { get; } = fieldLines;

    public byte[] Body { get; } = body;

    public FieldLine? FindFieldLine(string fieldName)
    {
        foreach (var fieldLine in FieldLines)
        {
            if (string.Compare(fieldLine.FieldName, fieldName, ignoreCase: true) == 0)
            {
                return fieldLine;
            }
        }
        return null;
    }
}

enum LWSTag { Normal, Continuation }

class RequestParser
{
    private const byte CR = 13;
    private const byte LF = 10;
    private const byte SP = 32;
    private const byte HT = 9;

    private readonly List<byte> request;
    private int index;
    private byte?[] octet = new byte?[3];

    private RequestParser(List<byte> request)
    {
        this.request = request;
        index = -1;
        Advance();
        Advance();
        Advance();
    }

    public static Request Parse(List<byte> request)
    {
        var parser = new RequestParser(request);
        return parser.Request();
    }

    private Request Request()
    {
        var requestLine = RequestLine();
        var fieldLines = FieldLines();
        var body = Body();
        return new Request(requestLine, fieldLines, body);
    }

    private RequestLine RequestLine()
    {
        var method = new List<Byte>();
        while (octet[0] is byte o && o != SP)
        {
            method.Add(o);
            Advance();
        }

        Expect(SP);

        var requestURI = new List<byte>();
        while (octet[0] is byte o && o != SP)
        {
            requestURI.Add(o);
            Advance();
        }

        Expect(SP);

        var httpVersion = new List<byte>();
        while (octet[0] is byte o && (octet[0] != CR || octet[1] != LF))
        {
            httpVersion.Add(o);
            Advance();
        }

        Expect(CR);
        Expect(LF);

        return new RequestLine(GetString(method), GetString(requestURI), GetString(httpVersion));
    }

    private List<FieldLine> FieldLines()
    {
        var fieldLines = new List<FieldLine>();

        while (octet[0] is byte && !IsCRLF(octet[0], octet[1]))
        {
            var fieldName = ExpectToken();

            Expect(':');

            var fieldValue = new List<byte>();
            while (octet[0] is byte o && !IsCRLF(octet[0], octet[1]))
            {
                fieldValue.Add(o);
                Advance();
            }
            Expect(CR);
            Expect(LF);

            var fieldLine = new FieldLine(GetString(fieldName), GetString(fieldValue).Trim());
            fieldLines.Add(fieldLine);
        }

        Expect(CR);
        Expect(LF);

        return fieldLines;
    }

    private byte[] Body()
    {
        var body = new List<byte>();
        while (octet[0] is byte o)
        {
            body.Add(o);
            Advance();
        }
        return body.ToArray();
    }

    private void Expect(byte o)
    {
        if (octet[0] == o)
        {
            Advance();
        }
        else
        {
            throw new Exception($"unexpected octet {octet[0]}, expected {o}");
        }
    }

    private void Expect(char c)
    {
        Expect((byte)c);
    }

    private List<byte> ExpectToken()
    {
        if (octet[0] is byte o && IsToken(o))
        {
            return Token();
        }
        else
        {
            throw new Exception($"expected token but found {octet[0]}");
        }
    }

    private List<byte> Token()
    {
        var token = new List<byte>();
        while (octet[0] is byte o && IsToken(o))
        {
            token.Add(o);
            Advance();
        }
        return token;
    }

    /// <summary>
    /// Advance once octet in the request.
    /// </summary>
    private void Advance()
    {
        index++;
        octet[0] = octet[1];
        octet[1] = octet[2];
        octet[2] = index < request.Count ? request[index] : null;
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
}
