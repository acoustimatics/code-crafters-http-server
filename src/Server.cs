using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

var requestHandlers = new Func<string, string?>[]
{
    RequestDefault,
    RequestEcho,
};

using var server = new TcpListener(IPAddress.Any, 4221);
server.Start();

while (true)
{
    using var socket = server.AcceptSocket();

    var buffer = new byte[1024];
    var bytesReceived = 0;
    var request = "";
    do
    {
        bytesReceived = socket.Receive(buffer);
        request += Encoding.ASCII.GetString(buffer, index: 0, count: bytesReceived);
    }
    while (socket.Available > 0);

    var requestLines = request.Split("\r\n");
    var requestLine = requestLines[0];
    var requestLineElements = requestLine.Split(' ');
    var requestTarget = requestLineElements[1];

    Console.WriteLine(requestLine);

    string? response = null;
    foreach (var requestHandler in requestHandlers)
    {
        response = requestHandler(requestTarget);
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

void Send(Socket socket, string response)
{
    var responseBytes = Encoding.ASCII.GetBytes(response);
    socket.Send(responseBytes);
}

string? RequestDefault(string requestTarget)
{
    var regex = new Regex("^/$");
    var match = regex.Match(requestTarget);
    if (!match.Success)
    {
        return null;
    }
    return $"HTTP/1.1 200 OK\r\n\r\n";
}

string? RequestEcho(string requestTarget)
{
    var regex = new Regex("^/echo/(?<str>[^/]+)$");
    var match = regex.Match(requestTarget);
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

