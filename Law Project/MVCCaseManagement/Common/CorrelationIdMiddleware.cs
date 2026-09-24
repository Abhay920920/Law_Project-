using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MVCCaseManagement.Common
{
    /// <summary>
    /// Attaches an X-Correlation-ID header to every request and response, creating a traceable log scope.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if incoming request already carries a correlation ID (e.g. from API gateway or load balancer)
            if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
            }

            // Set correlation ID on response headers
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                {
                    context.Response.Headers[CorrelationIdHeader] = correlationId;
                }
                return Task.CompletedTask;
            });

            // Store in HttpContext items for easy controller retrieval
            context.Items[CorrelationIdHeader] = correlationId.ToString();

            // Push to log scope for all downstream logging
            using (_logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId.ToString(),
                ["RequestPath"] = context.Request.Path.Value ?? "",
                ["RequestMethod"] = context.Request.Method
            }))
            {
                await _next(context);
            }
        }
    }
}
