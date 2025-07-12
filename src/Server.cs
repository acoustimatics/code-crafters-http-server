using System.Net;
using System.Net.Sockets;
using System.Text;

using var server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("started");

while (true)
{
    using var socket = server.AcceptSocket();
    Console.WriteLine("[accepted socket]");

    var buffer = new byte[1024];
    var bytesReceived = 0;
    var request = "";
    do
    {
        Console.WriteLine("[receiving bytes]");
        bytesReceived = socket.Receive(buffer);
        Console.WriteLine($"[received {bytesReceived} bytes]");

        request += Encoding.ASCII.GetString(buffer, index: 0, count: bytesReceived);
    }
    while (socket.Available > 0);

    Console.WriteLine("[received request]");
    Console.Write(request);

    var requestLines = request.Split("\r\n");
    var requestLine = requestLines[0];
    var requestLineElements = requestLine.Split(' ');
    var requestTarget = requestLineElements[1];
    Console.WriteLine($"[message target] {requestTarget}");

    string responseStatus;
    if (requestTarget == "/")
    {
        responseStatus = "200 OK";
    }
    else
    {
        responseStatus = "400 Not Found";
    }

    var responseString = $"HTTP/1.1 {responseStatus}\r\n\r\n";
    Console.WriteLine($"[response is] {responseString}");

    Console.WriteLine("[sending response]");
    var response = Encoding.ASCII.GetBytes(responseString);
    socket.Send(response);
    Console.WriteLine("[response sent]");
}
