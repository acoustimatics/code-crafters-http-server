using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using static Util;

class Program
{
    public static async Task Main(string[] args)
    {
        CommandLine.Parse(args);
        Log("directory: ", Options.Instance.Directory ?? "<NONE>");

        var requestHandlers = new Func<Request, Response?>[]
        {
            RequestDefault,
            RequestEcho,
            RequestAgent,
            RequestFiles,
            RequestFilesPost,
        };

        using var server = new TcpListener(IPAddress.Any, 4221);
        server.Start();

        var connectionCounter = 0;

        while (true)
        {
            Log("waiting for connections");
            var socket = await server.AcceptSocketAsync();

            var connectionNumber = connectionCounter++;
            Log($"accepted connection #{connectionNumber}");

            _ = HandleConnection(socket, requestHandlers).ContinueWith(task => {
                if (task.IsFaulted)
                {
                    Log($"connection #{connectionNumber} faulted with: {task.Exception.Message}");
                }
                else
                {
                    Log($"finished connection #{connectionNumber}");
                }
            });
        }
    }

    static async Task HandleConnection(Socket socket, Func<Request, Response?>[] requestHandlers)
    {
        using var parser = new RequestParser(socket);

        while (true)
        {
            var request = await parser.ParseRequest();

            Log("request target: ", request.RequestLine.RequestTarget);

            var response = requestHandlers
                .Select(requestHandler => requestHandler(request))
                .FirstOrDefault(response => response != null)
                ?? new Response { Status = HttpStatus.NotFound };

            await ProcessCompression(request, response);

            await response.RenderAsync(socket);

            Log("responded to: ", request.RequestLine.RequestTarget);
        }
    }

    static async Task<List<byte>> ReadRequest(Socket socket)
    {
        var buffer = new byte[64];
        var request = new List<byte>();
        do
        {
            var bytesReceived = await socket.ReceiveAsync(buffer);
            for (var i = 0; i < bytesReceived; i++)
            {
                request.Add(buffer[i]);
            }
        }
        while (socket.Available > 0);
        return request;
    }

    static async Task ProcessCompression(Request request, Response response)
    {
        var acceptEncoding = request.GetFieldLineValue("Accept-Encoding");
        if (acceptEncoding == null)
        {
            return;
        }

        var acceptsGzip = ParseCommaSeparatedList(acceptEncoding)
            .Any(item => item.Trim() == "gzip");
        if (!acceptsGzip)
        {
            return;
        }

        var content = await GzipCompress(response.Content);

        response.Content = content;
        response.Headers["Content-Length"] = content.Length.ToString();
        response.Headers["Content-Encoding"] = "gzip";
    }

    static Response? RequestDefault(Request request)
    {
        var regex = new Regex("^/$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }

        return new Response { Status = HttpStatus.Ok };
    }

    static Response? RequestEcho(Request request)
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

        var content = Encoding.ASCII.GetBytes(str);

        return new Response
        {
            Status = HttpStatus.Ok,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/plain",
                ["Content-Length"] = $"{content.Length}",
            },
            Content = content,
        };
    }

    static Response? RequestFiles(Request request)
    {
        if (request.RequestLine.Method != "GET")
        {
            return null;
        }

        var regex = new Regex("^/files/(?<filename>[^/]+)$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }
        Log("Request for file");
        
        var directory = Options.Instance.Directory;
        if (string.IsNullOrEmpty(directory))
        {
            Log("Directory not set.");
            return null;
        }

        var filename = match.Groups["filename"]?.Value;
        if (filename == null)
        {
            return null;
        }

        var path = Path.Combine(directory, filename);
        Log("Requesting file: ", path);

        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            Log("exception: ", ex.Message);
            return null;
        }

        Log("Sending file: ", filename);
        
        return new Response
        {
            Status = HttpStatus.Ok,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/octet-stream",
                ["Content-Length"] = $"{content.Length}",
            },
            Content = content,
        };
    }

    static Response? RequestFilesPost(Request request)
    {
        if (request.RequestLine.Method != "POST")
        {
            return null;
        }

        var regex = new Regex("^/files/(?<filename>[^/]+)$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }
        Log("Posting a file.");
        
        var directory = Options.Instance.Directory;
        if (string.IsNullOrEmpty(directory))
        {
            Log("Directory not set.");
            return null;
        }

        var filename = match.Groups["filename"]?.Value;
        if (filename == null)
        {
            return null;
        }

        var path = Path.Combine(directory, filename);
        Log("Saving to file: ", path);

        try
        {
            File.WriteAllBytes(path, request.Body);
        }
        catch (Exception ex)
        {
            Log("exception: ", ex.Message);
            return null;
        }

        Log("Sending file: ", filename);

        return new Response { Status = HttpStatus.Created };
    }

    static Response? RequestAgent(Request request)
    {
        var regex = new Regex("^/user-agent$");
        var match = regex.Match(request.RequestLine.RequestTarget);
        if (!match.Success)
        {
            return null;
        }

        var userAgent = request.GetFieldLineValue("User-Agent");

        var content = Encoding.ASCII.GetBytes(userAgent ?? "");
        
        return new Response
        {
            Status = HttpStatus.Ok,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/plain",
                ["Content-Length"] = $"{content.Length}",
            },
            Content = content,
        };
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
