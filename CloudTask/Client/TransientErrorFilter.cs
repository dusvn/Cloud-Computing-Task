using System.Fabric;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Client
{
    /// <summary>
    /// Turns temporary backend failures into status codes the frontend may retry:
    /// 504 when a service call timed out, 503 when a service/partition is not reachable right now
    /// (failover, primary moving, service starting). Anything else stays a 500 and is not retried.
    /// Retrying is safe because every purchase carries its idempotency key.
    /// </summary>
    internal sealed class TransientErrorFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            int? status = null;
            // remoting may wrap the real cause (AggregateException / InnerException): check the whole chain
            for (Exception? e = context.Exception; e is not null && status is null; e = e.InnerException)
                status = Classify(e, context.HttpContext.RequestAborted.IsCancellationRequested);

            if (status is null) return;   // not transient: ASP.NET returns 500

            context.Result = new ObjectResult(new PurchaseResponse
            {
                StatusCode = status.Value,
                Message = status == StatusCodes.Status504GatewayTimeout
                    ? "The service did not answer in time. Please try again."
                    : "The service is temporarily unavailable. Please try again."
            })
            { StatusCode = status };
            context.ExceptionHandled = true;
        }

        private static int? Classify(Exception e, bool clientGone) => e switch
        {
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            OperationCanceledException when !clientGone => StatusCodes.Status504GatewayTimeout,
            FabricTransientException or FabricNotPrimaryException or FabricNotReadableException
                or FabricServiceNotFoundException or FabricObjectClosedException
                => StatusCodes.Status503ServiceUnavailable,
            _ => null
        };
    }
}
