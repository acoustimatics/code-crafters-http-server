using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

enum HttpStatus
{
    Ok = 200,
    Created = 201,
    NotFound = 404,
}

class Response
{
    public required HttpStatus Status { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public byte[] Content { get; set; } = new byte[0];
}

static class ResponseRendering
{
    public static async Task RenderAsync(this Response response, Socket socket)
    {
        await RenderResponseLineAsync(socket, response.Status);
        await RenderHeadersAsync(socket, response.Headers);
        await socket.SendAsync(response.Content);
    }

    static async Task RenderResponseLineAsync(Socket socket, HttpStatus status)
    {
        var statusString = status switch
        {
            HttpStatus.Ok => "OK",
            HttpStatus.Created => "Created",
            HttpStatus.NotFound => "Not Found",
            _ => "Unknown",
        };

        await SendAsciiAsync(socket, $"HTTP/1.1 {(int)status} {statusString}\r\n");
    }

    static async Task RenderHeadersAsync(Socket socket, Dictionary<string, string> headers)
    {
        foreach (var kvp in headers)
        {
            await SendAsciiAsync(socket, $"{kvp.Key}: {kvp.Value}\r\n");
        }
        await SendAsciiAsync(socket, "\r\n");
    }

    static async Task SendAsciiAsync(Socket socket, string str)
    {
        var bytes = Encoding.ASCII.GetBytes(str);
        await socket.SendAsync(bytes);
    }
}
