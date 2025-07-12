using System.Net;
using System.Net.Sockets;
using System.Text;

using var server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("started");

using var socket = server.AcceptSocket();
Console.WriteLine("accepted socket");

var buffer = new byte[1024];
var bytesReceived = 0;
do
{
    Console.WriteLine("receiving bytes");
    bytesReceived = socket.Receive(buffer);
    Console.WriteLine($"received {bytesReceived} bytes");
}
while (socket.Available > 0);

Console.WriteLine("sending response");
var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n");
socket.Send(response);
Console.WriteLine("response sent");
Console.WriteLine("exiting");