using System.Text;

record struct Span(int Start, int Length);

record RequestLine(
    string Method,
    string RequestTarget,
    string HttpVersion);

record FieldLine(
    string FieldName,
    string FieldValue);

record Request(
    RequestLine RequestLine,
    List<FieldLine> FieldLines);

enum TokenTag
{
    Delimiter,
    EndOfText,
    Whitespace,
    Token,
}

readonly struct Token(TokenTag tag, Span spanLexeme)
{
    public TokenTag Tag { get; } = tag;
    public Span SpanLexeme { get; } = spanLexeme;
}

class RequestScanner
{
    private readonly string requestText;
    private int pos;
    private char? ch;

    public RequestScanner(string requestText)
    {
        this.requestText = requestText;
        pos = -1;
        ch = null;

        Advance();
    }

    private bool IsTokenChar(char c)
    {
        return char.IsAsciiLetterOrDigit(c) ||
            c == '!' ||
            c == '#' ||
            c == '$' ||
            c == '%' ||
            c == '&' ||
            c == '\'' ||
            c == '*' ||
            c == '+' ||
            c == '-' ||
            c == '.' ||
            c == '^' ||
            c == '_' ||
            c == '`' ||
            c == '|' ||
            c == '~';
    }

    private bool IsWhitespace(char c)
    {
        return c == ' ' || c == '\t' || c == '\r' || c == '\n';
    }

    private bool IsDelimiter(char c)
    {
        return c == ':' || c == '?' || c == '/';
    }

    private void Advance()
    {
#if PARSER_DEBUG
        if (pos >= 0) {
            switch (ch)
            {
                case null:
                    Console.WriteLine($"[{pos}: NULL]");
                    break;

                case ' ':
                    Console.WriteLine($"[{pos}: SP]");
                    break;

                case '\r':
                    Console.WriteLine($"[{pos}: CR]");
                    break;

                case '\n':
                    Console.WriteLine($"[{pos}: LF]");
                    break;

                case '\t':
                    Console.WriteLine($"[{pos}: HT]");
                    break;

                case char c:
                    Console.WriteLine($"[{pos}: `{c}`]");
                    break;
            }
        }
#endif

        pos++;
        if (pos < requestText.Length)
        {
            ch = requestText[pos];
        }
        else
        {
            ch = null;
        }
    }

    private Token AcceptCharacter(TokenTag tag)
    {
        var token = new Token(tag, new Span(pos, 1));
        Advance();
        return token;
    }

    private Token AcceptToken()
    {
        var start = pos;
        while (ch != null && IsTokenChar(ch.Value))
        {
            Advance();
        }
        return new Token(TokenTag.Token, new Span(start, pos - start));
    }

    public Token NextToken()
    {
        switch (ch)
        {
            case null:
                return new Token(TokenTag.EndOfText, new Span(0, 0));

            case char c when IsWhitespace(c):
                return AcceptCharacter(TokenTag.Whitespace);
                
            case char c when IsDelimiter(c):
                return AcceptCharacter(TokenTag.Delimiter);

            case char c when IsTokenChar(c):
                return AcceptToken();

            case char c:
                throw new Exception($"unexpected character `{c}`");
        }
    }
}

class RequestParser
{
    private readonly string requestText;
    private readonly RequestScanner scanner;
    private Token current;

    private RequestParser(string requestText)
    {
        this.requestText = requestText;
        scanner = new RequestScanner(requestText);
        Advance();
    }

    private void Advance()
    {
        current = scanner.NextToken();

#if PARSER_DEBUG
        if (current.Tag == TokenTag.Token)
        {
            var lexeme = requestText.Substring(current.SpanLexeme.Start, current.SpanLexeme.Length);
            Console.WriteLine($"<{current.Tag}: `{lexeme}`>");
        }
        else
        {
            Console.WriteLine($"<{current.Tag}>");
        }
#endif
    }

    private void Expect(TokenTag tag)
    {
        if (current.Tag != tag)
        {
            throw new Exception($"expected {tag} but got {current.Tag}");
        }

        Advance();
    }

    private bool Match(TokenTag tag, char c)
    {
        var isMatch = current.Tag == tag && requestText[current.SpanLexeme.Start] == c;
        if (isMatch)
        {
            Advance();
        }
        return isMatch;
    }

    private string? MatchToken()
    {
        if (current.Tag != TokenTag.Token)
        {
            return null;
        }

        var word = requestText.Substring(current.SpanLexeme.Start, current.SpanLexeme.Length);
        Advance();
        return word;
    }

    private string ExpectToken()
    {
        if (MatchToken() is string token)
        {
            return token;
        }

        throw new Exception($"expected Token but got {current.Tag}");
    }

    private string WhileNotWhitespace()
    {
        var str = new StringBuilder();
        while (current.Tag != TokenTag.Whitespace)
        {
            str.Append(
                requestText.AsSpan().Slice(
                    current.SpanLexeme.Start,
                    current.SpanLexeme.Length));

            Advance();
        }
        return str.ToString();
    }

    public static Request Parse(string requestText)
    {
        var parser = new RequestParser(requestText);
        return parser.Request();
    }

    private Request Request()
    {
        var requestLine = RequestLine();
        CRLF();
        var fieldLines = FieldLines();
        CRLF();
        return new Request(requestLine, fieldLines);
    }

    private RequestLine RequestLine()
    {
        var method = ExpectToken();
        SP();
        var requestTarget = WhileNotWhitespace();
        SP();
        var httpVersion = WhileNotWhitespace();
        
        return new RequestLine(method, requestTarget, httpVersion);
    }

    private List<FieldLine> FieldLines()
    {
        var fieldLines = new List<FieldLine>();
        while (FieldLine() is FieldLine fieldLine)
        {
            fieldLines.Add(fieldLine);
            CRLF();
        }
        return fieldLines;
    }

    private FieldLine? FieldLine()
    {
        if (MatchToken() is string fieldName)
        {
            Match(TokenTag.Delimiter, ':');
            OWS();
            var fieldValue = WhileNotWhitespace();
            OWS();

            return new FieldLine(fieldName, fieldValue);
        }

        return null;
    }

    private void SP()
    {
        if (!Match(TokenTag.Whitespace, ' '))
        {
            throw new Exception("expected SP");
        }
    }

    private void CRLF()
    {
        if (!Match(TokenTag.Whitespace, '\r') || !Match(TokenTag.Whitespace, '\n'))
        {
            throw new Exception("expected CRLF");
        }
    }

    private void OWS()
    {
        while (Match(TokenTag.Whitespace, ' ') || Match(TokenTag.Whitespace, '\t'))
        {
        }
    }
}

