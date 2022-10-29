using GSPlatformBackServer.Data;
using Microsoft.Net.Http.Headers;

namespace GSPlatformBackServer.Helpers
{
    internal class HttpExceptionMiddleware
    {
        private readonly RequestDelegate next;

        public HttpExceptionMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next.Invoke(context);
            }
            catch (HttpResponseException e)
            {
                var response = context.Response;
                if (response.HasStarted)
                {
                    //throw;
                }
                response.StatusCode = (int)e.StatusCode;
            }
            catch (InvalidApiUsageException e)
            {
                var response = context.Response;
                if (response.HasStarted)
                {
                    //throw;
                }
                response.StatusCode = StatusCodes.Status400BadRequest;
                await LogHelpers.WriteLogAsync(context, LogCategoryNames.InvalidRequest + ":" + e.Reason, e.UserToken);
            }
            catch
            {
                var response = context.Response;
                if (response.HasStarted)
                {
                    //throw;
                }
                response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}
