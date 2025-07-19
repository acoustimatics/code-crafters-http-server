using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    public static async Task Main(string[] args)
    {
        var requestHandlers = new Func<Request, string?>[]
        {
            RequestDefault,
            RequestEcho,
            RequestAgent,
        };

        using var server = new TcpListener(IPAddress.Any, 4221);
        server.Start();

        var acceptTask = server.AcceptSocketAsync();

        var connectionTasks = new List<Task>();

        while (true)
        {
            if (acceptTask.IsCompleted)
            {
                var socket = acceptTask.Result;
                acceptTask = server.AcceptSocketAsync();
                Log("accepted connection");

                var task = HandleConnection(socket, requestHandlers);
                connectionTasks.Add(task);
            }

            for (var i = connectionTasks.Count - 1; i >= 0; i--)
            {
                if (connectionTasks[i].IsCompleted)
                {
                    connectionTasks.RemoveAt(i);
                }
            }
        }
    }

    static async Task HandleConnection(Socket socket, Func<Request, string?>[] requestHandlers)
    {
        try
        {
            var requestText = await ReadRequestText(socket);
            var request = RequestParser.Parse(requestText);

            Log("request target: ", request.RequestLine.RequestTarget);

            string? response = null;
            foreach (var requestHandler in requestHandlers)
            {
                response = requestHandler(request);
                if (response != null)
                {
                    await Send(socket, response);
                    break;
                }
            }

            if (response == null)
            {
                await Send(socket, $"HTTP/1.1 404 Not Found\r\n\r\n");
            }

            Log("responded to: ", request.RequestLine.RequestTarget);
        }
        finally
        {
            socket.Dispose();
        }
    }

    static async Task<string> ReadRequestText(Socket socket)
        {
            var buffer = new byte[64];
            var requestText = new StringBuilder();
            do
            {
                var bytesReceived = await socket.ReceiveAsync(buffer);
                var textReceived = Encoding.ASCII.GetString(buffer, index: 0, count: bytesReceived);
                requestText.Append(textReceived);
            }
            while (socket.Available > 0);
            return requestText.ToString();
        }

    static async Task Send(Socket socket, string response)
    {
        var responseBytes = Encoding.ASCII.GetBytes(response);
        await socket.SendAsync(responseBytes);
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

    static void Log(string message)
    {
        Console.WriteLine(message);
    }

    static void Log(string message, string context)
    {
        Console.Write(message);
        Console.WriteLine(context);
    }
}
