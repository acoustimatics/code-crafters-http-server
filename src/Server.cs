using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    public static void Main(string[] args)
    {
        var requestHandlers = new Func<Request, string?>[]
        {
            RequestDefault,
            RequestEcho,
            RequestAgent,
        };

        using var server = new TcpListener(IPAddress.Any, 4221);
        server.Start();

        while (true)
        {
            using var socket = server.AcceptSocket();

            var requestText = ReadRequestText(socket);
            var request = RequestParser.Parse(requestText);

            Console.WriteLine(request);

            string? response = null;
            foreach (var requestHandler in requestHandlers)
            {
                response = requestHandler(request);
                if (response != null)
                {
                    Send(socket, response);
                    break;
                }
            }

            if (response == null)
            {
                Send(socket, $"HTTP/1.1 404 Not Found\r\n\r\n");
            }
        }
    }

    static string ReadRequestText(Socket socket)
    {
        var buffer = new byte[64];
        var requestText = new StringBuilder();
        do
        {
            var bytesReceived = socket.Receive(buffer);
            var textReceived = Encoding.ASCII.GetString(buffer, index: 0, count: bytesReceived);
            requestText.Append(textReceived);
        }
        while (socket.Available > 0);
        return requestText.ToString();
    }

    static void Send(Socket socket, string response)
    {
        var responseBytes = Encoding.ASCII.GetBytes(response);
        socket.Send(responseBytes);
    }

    static string? RequestDefault(Request request)
    {
        var regex = new Regex("^/$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }
        return $"HTTP/1.1 200 OK\r\n\r\n";
    }

    static string? RequestEcho(Request request)
    {
        var regex = new Regex("^/echo/(?<str>[^/]+)$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }

        var str = match.Groups["str"]?.Value;
        if (str == null)
        {
            return null;
        }

        var response = new StringBuilder();

        response.Append("HTTP/1.1 200 OK\r\n");

        response.Append("Content-Type: text/plain\r\n");

        var contentLength = Encoding.ASCII.GetByteCount(str);
        response.Append("Content-Length: ").Append(contentLength).Append("\r\n");

        response.Append("\r\n");

        response.Append(str);

        return response.ToString();
    }

    static string? RequestAgent(Request request)
    {
        var regex = new Regex("^/user-agent$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }

        var body = "";

        foreach (var fieldLine in request.FieldLines)
        {
            if (string.Compare(fieldLine.FieldName, "User-Agent", ignoreCase: true) == 0)
            {
                body = fieldLine.FieldValue;
                break;
            }
        }

        var response = new StringBuilder();

        response.Append("HTTP/1.1 200 OK\r\n");

        response.Append("Content-Type: text/plain\r\n");

        var contentLength = Encoding.ASCII.GetByteCount(body);
        response.Append("Content-Length: ").Append(contentLength).Append("\r\n");

        response.Append("\r\n");

        response.Append(body);

        return response.ToString();
    }
}
