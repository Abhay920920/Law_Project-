using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MVCCaseManagement.Common
{
    /// <summary>
    /// Global action filter that enforces strict read-only access for Division Controller (DC) users.
    /// Blocks state-modifying HTTP verbs (POST, PUT, DELETE, PATCH) and GET form endpoints (Create, Edit, Register, etc.)
    /// while preserving authentication, AI legal assistant queries, and read-only searches.
    /// </summary>
    public class DivisionControllerReadOnlyFilter : IActionFilter
    {
        private static readonly HashSet<string> AllowedActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Login",
            "Logout",
            "ChangePassword",
            "ForgotPassword",
            "Ask",
            "GetChatHistory",
            "DismissTransferNotification",
            "GetCaseJson",
            "GetPayments",
            "GetLinkedCases",
            "GetAdvocates",
            "DetailsPartial",
            "SearchTransferCase"
        };

        private static readonly HashSet<string> AllowedControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Account",
            "NyayaPatha"
        };

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var filterRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? user.FindFirst("Role")?.Value ?? "";
                var filterFullName = user.FindFirst("FullName")?.Value ?? "";
                var filterUsername = user.Identity?.Name ?? "";

                bool isDC = user.IsInRole("Division Controller") || 
                            user.IsInRole("DC") ||
                            string.Equals(filterRole, "Division Controller", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(filterRole, "DC", StringComparison.OrdinalIgnoreCase) ||
                            filterRole.Contains("Division Controller", StringComparison.OrdinalIgnoreCase) ||
                            filterUsername.StartsWith("DC_", StringComparison.OrdinalIgnoreCase) ||
                            filterFullName.Contains("Division Controller", StringComparison.OrdinalIgnoreCase);

                if (!isDC) return;
                var httpMethod = context.HttpContext.Request.Method;
                var actionName = context.RouteData.Values["action"]?.ToString() ?? "";
                var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";

                // 1. Block state-modifying requests (POST, PUT, DELETE, PATCH)
                if (!HttpMethods.IsGet(httpMethod) && !HttpMethods.IsHead(httpMethod))
                {
                    if (AllowedControllers.Contains(controllerName) || AllowedActionNames.Contains(actionName))
                    {
                        return; // Permitted account or read-only/AI post
                    }

                    bool isAjaxOrJson = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                        (context.HttpContext.Request.ContentType?.Contains("application/json") == true) ||
                                        (context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json"));

                    if (isAjaxOrJson)
                    {
                        context.Result = new JsonResult(new
                        {
                            success = false,
                            message = "Access Denied: Division Controller is a view-only role. Modification actions are strictly disabled."
                        })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                    }
                    else
                    {
                        context.Result = new ForbidResult();
                    }
                    return;
                }

                // 2. Block GET actions that open create/edit/manage forms
                if (actionName.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
                    actionName.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ||
                    actionName.StartsWith("Register", StringComparison.OrdinalIgnoreCase) ||
                    actionName.StartsWith("Manage", StringComparison.OrdinalIgnoreCase) ||
                    actionName.StartsWith("Transfer", StringComparison.OrdinalIgnoreCase) ||
                    actionName.StartsWith("StayCompliance", StringComparison.OrdinalIgnoreCase) ||
                    actionName.Equals("RemindBackEntry", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new ForbidResult();
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
