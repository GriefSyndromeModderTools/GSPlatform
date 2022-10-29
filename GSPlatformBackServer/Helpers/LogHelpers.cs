using GSPlatformBackServer.Data;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace GSPlatformBackServer.Helpers
{
    internal sealed class InvalidApiUsageException : Exception
    {
        public string Reason { get; }
        public string? UserToken { get; }

        public InvalidApiUsageException(string reason, string? userToken = null)
        {
            Reason = reason;
            UserToken = userToken;
        }
    }

    internal sealed class HttpResponseException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public HttpResponseException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }
    }

    internal static class LogHelpers
    {
        public static Task WriteLogAsync(HttpContext context, string logContent, string? userToken)
        {
            //TODO if we are in busy state, pause less important logs
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            db.LogRecords.Add(new LogRecord()
            {
                Created = DateTime.Now,
                UserToken = userToken ?? string.Empty,
                Request = context.Request.Path.Value ?? string.Empty,
                RequestServer = ServerInfo.ThisServerName,
                RequestClient = context.GetIPEndPoint().ToString(),
                Content = logContent,
            });
            return db.SaveChangesAsync();
        }

        public static void WriteLog(IPEndPoint ep, string logContent, string? userToken)
        {
            //TODO if we are in busy state, pause less important logs
            //TODO
        }

        //TODO remove
        //moved to middleware
        public static void UseLogHelperExceptionFilter(this IApplicationBuilder builder)
        {
            builder.UseExceptionHandler(new ExceptionHandlerOptions()
            {
                ExceptionHandler = context =>
                {
                    var exceptionDetails = context.Features.Get<IExceptionHandlerFeature>();
                    var ex = exceptionDetails?.Error;
                    if (ex is HttpResponseException r)
                    {
                        context.Response.StatusCode = (int)r.StatusCode;
                        return Task.CompletedTask;
                    }
                    else if (ex is InvalidApiUsageException ia)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return WriteLogAsync(context, LogCategoryNames.InvalidRequest + ":" + ia.Reason, ia.UserToken);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return Task.CompletedTask;
                    }
                },
                AllowStatusCode404Response = true,
            });
        }
    }
}
