using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    public static Task Main(string[] args)
    {
        CommandLine.Parse(args);
        Log("directory: ", Options.Instance.Directory ?? "<NONE>");

        var requestHandlers = new Func<Request, Socket, Task<bool>>[]
        {
            RequestDefault,
            RequestEcho,
            RequestAgent,
            RequestFiles,
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

    static async Task HandleConnection(Socket socket, Func<Request, Socket, Task<bool>>[] requestHandlers)
    {
        try
        {
            var requestText = await ReadRequestText(socket);
            var request = RequestParser.Parse(requestText);

            Log("request target: ", request.RequestLine.RequestTarget);

            string? response = null;
            foreach (var requestHandler in requestHandlers)
            {
                if (await requestHandler(request, socket))
                {
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

    static async Task WriteAscii(Socket socket, string str)
    {
        var bytes = Encoding.ASCII.GetBytes(str);
        await socket.SendAsync(bytes);
    }

    static async Task<bool> RequestDefault(Request request, Socket socket)
    {
        var regex = new Regex("^/$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return false;
        }
        await WriteAscii(socket, "HTTP/1.1 200 OK\r\n\r\n");
        return true;
    }

    static async Task<bool> RequestEcho(Request request, Socket socket)
    {
        var regex = new Regex("^/echo/(?<str>[^/]+)$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return false;
        }

        var str = match.Groups["str"]?.Value;
        if (str == null)
        {
            return false;
        }


        await WriteAscii(socket, "HTTP/1.1 200 OK\r\n");

        await WriteAscii(socket, "Content-Type: text/plain\r\n");

        var contentLength = Encoding.ASCII.GetByteCount(str);
        await WriteAscii(socket, $"Content-Length: {contentLength}\r\n");

        await WriteAscii(socket, "\r\n");

        await WriteAscii(socket, str);

        return true;
    }

    static async Task<bool> RequestFiles(Request request, Socket socket)
    {
        var regex = new Regex("^/files/(?<filename>[^/]+)$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return false;
        }
        Log("Request for file");
        
        var directory = Options.Instance.Directory;
        if (string.IsNullOrEmpty(directory))
        {
            Log("Directory not set.");
            return false;
        }

        var filename = match.Groups["filename"]?.Value;
        if (filename == null)
        {
            return false;
        }

        var path = Path.Combine(directory, filename);
        Log("Requesting file: ", path);

        byte[] fileContent;
        try
        {
            fileContent = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            Log("exception: ", ex.Message);
            return false;
        }

        Log("Sending file: ", filename);
        
        await WriteAscii(socket, "HTTP/1.1 200 OK\r\n");
        await WriteAscii(socket ,"Content-Type: application/octet-stream\r\n");
        await WriteAscii(socket, $"Content-Length: {fileContent.Length}\r\n");
        await WriteAscii(socket ,"\r\n");
        await socket.SendAsync(fileContent);

        return false;
    }

    static async Task<bool> RequestAgent(Request request, Socket socket)
    {
        var regex = new Regex("^/user-agent$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return false;
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

        await WriteAscii(socket, "HTTP/1.1 200 OK\r\n");
        await WriteAscii(socket, "Content-Type: text/plain\r\n");
        var contentLength = Encoding.ASCII.GetByteCount(body);
        await WriteAscii(socket, $"Content-Length: {contentLength}\r\n");
        await WriteAscii(socket, "\r\n");
        await WriteAscii(socket, body);

        return true;
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
