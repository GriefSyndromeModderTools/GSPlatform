using GSPlatformBackServer;
using GSPlatformBackServer.Data;
using GSPlatformBackServer.Helpers;
using GSPlatformBackServer.RoomServer;
using Microsoft.EntityFrameworkCore;

if (CLICommands.ProcessArgs(args))
{
    return;
}

var builder = WebApplication.CreateBuilder(args);

ServerInfo.LoadSettings(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddHostedService<MainRoomServer>();
builder.Services.AddHostedService<AuxRoomServer>();
builder.WebHost.UseUrls("http://127.0.0.1:5000");

var app = builder.Build();

if (builder.Configuration.GetSection("UseHttps").Value.ToLowerInvariant() == "true")
{
    app.UseHttpsRedirection();
}
//app.UseLogHelperExceptionFilter();
app.UseMiddleware<HttpExceptionMiddleware>();
app.UseAuthorization();
app.MapControllers();

new AppDbContext().Database.Migrate();
app.Run();
