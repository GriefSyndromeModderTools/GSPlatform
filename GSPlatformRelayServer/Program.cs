using GSPlatformRelayServer.RelayServer;
using Microsoft.AspNetCore.Mvc;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var httpAddress = bool.Parse(builder.Configuration.GetSection("AllowExternalHttp").Value) ? IPAddress.Any : IPAddress.Loopback;
var httpPort = int.Parse(builder.Configuration.GetSection("HttpPort").Value);
var udpPort = int.Parse(builder.Configuration.GetSection("RelayPort").Value);

RelayServer.Port = udpPort;
builder.Services.AddHostedService<RelayServer>();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(httpAddress, httpPort);
});

var app = builder.Build();
ConnectResponse response = new(udpPort);

app.MapPost("/connect.json", ([FromBody] ConnectRequest x) =>
{
    RelayServer.AddForward(IPAddress.Parse(x.IP1), x.Token1, IPAddress.Parse(x.IP2), x.Token2);
    return response;
});
app.Run();

record ConnectRequest(string IP1, ulong Token1, string IP2, ulong Token2);
record ConnectResponse(int Port);
