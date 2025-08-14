using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GEHistoricalImagery.Config
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public DateTime Timestamp { get; set; }

        public ApiResponse(T data, bool success = true)
        {
            Success = success;
            Data = data;
            Timestamp = DateTime.UtcNow;
        }
    }


    public class ApiResponseWrapperFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult objectResult)
            {
                if (objectResult.Value is not ApiResponse<object>)
                {
                    var wrapped = new ApiResponse<object>(
                        data: objectResult.Value,
                        success: (objectResult.StatusCode >= 200 && objectResult.StatusCode < 300)
                    );
                    context.Result = new ObjectResult(wrapped)
                    {
                        StatusCode = objectResult.StatusCode
                    };
                }
            }

            await next();
        }
    }
}