using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MVCCaseManagement.Common;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add logging configuration to prevent EventLog object disposal crashes on Windows background services
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Strongly-typed options registration
builder.Services.Configure<ECourtsOptions>(builder.Configuration.GetSection(ECourtsOptions.SectionName));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<TR18Options>(builder.Configuration.GetSection(TR18Options.SectionName));
builder.Services.Configure<MVCCaseManagement.Models.AI.NyayaPathaOptions>(builder.Configuration.GetSection(MVCCaseManagement.Models.AI.NyayaPathaOptions.SectionName));

// Add services to the container with global AntiForgeryToken validation on state-changing requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
}).AddRazorRuntimeCompilation();

builder.Services.AddMemoryCache(); // Used for login brute-force, OTP attempt limiting, and OAuth token caching
builder.Services.AddHealthChecks(); // Health check endpoints for production SRE monitoring

// Add Response Compression (Brotli & Gzip) for fast network speeds
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add repositories
builder.Services.AddSingleton<DBHelper>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMasterRepository, MasterRepository>();
builder.Services.AddScoped<ICaseRepository, CaseRepository>();
builder.Services.AddScoped<IAppealRepository, AppealRepository>();
builder.Services.AddScoped<IJudgementRepository, JudgementRepository>();
builder.Services.AddScoped<IEPRepository, EPRepository>();
builder.Services.AddScoped<IGratuityRepository, GratuityRepository>();
builder.Services.AddScoped<IPettyBillRepository, PettyBillRepository>();
builder.Services.AddScoped<ICasePaymentRepository, CasePaymentRepository>();
builder.Services.AddScoped<ILabourRepository, LabourRepository>();
builder.Services.AddScoped<IArisingApplicationRepository, ArisingApplicationRepository>();
builder.Services.AddScoped<ILabourEPRepository, LabourEPRepository>();
builder.Services.AddScoped<IOtherCourtsRepository, OtherCourtsRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IECourtsRepository, ECourtsRepository>();
builder.Services.AddScoped<ICaseNotingRepository, CaseNotingRepository>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MVCCaseManagement.Services.Audit.ICaseActivityLogger, MVCCaseManagement.Services.Audit.CaseActivityLogger>();
builder.Services.AddHostedService<MVCCaseManagement.Services.NotificationBackgroundService>();

// Nyaya Patha AI Legal Research Assistant Subsystem
builder.Services.Configure<MVCCaseManagement.Models.AI.AIOptions>(builder.Configuration.GetSection(MVCCaseManagement.Models.AI.AIOptions.SectionName));
builder.Services.Configure<MVCCaseManagement.Models.AI.NyayaPathaOptions>(builder.Configuration.GetSection(MVCCaseManagement.Models.AI.NyayaPathaOptions.SectionName));

// Local Ollama Inference & Intelligence Services
builder.Services.AddHttpClient<MVCCaseManagement.Services.AI.OllamaLLMProvider>();
builder.Services.AddSingleton<MVCCaseManagement.Services.AI.ILLMProvider>(sp => sp.GetRequiredService<MVCCaseManagement.Services.AI.OllamaLLMProvider>());
builder.Services.AddSingleton<MVCCaseManagement.Services.AI.ILLMService, MVCCaseManagement.Services.AI.LLMService>();

builder.Services.AddHttpClient<MVCCaseManagement.Services.AI.ILegalWebSearchService, MVCCaseManagement.Services.AI.LegalWebSearchService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IQueryRouterService, MVCCaseManagement.Services.AI.QueryRouterService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IDocumentTextExtractor, MVCCaseManagement.Services.AI.DocumentTextExtractor>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IDocumentSearchService, MVCCaseManagement.Services.AI.DocumentSearchService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IConflictDetectorService, MVCCaseManagement.Services.AI.ConflictDetectorService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.ISimilarCaseService, MVCCaseManagement.Services.AI.SimilarCaseService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IECourtsContextService, MVCCaseManagement.Services.AI.ECourtsContextService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.ILegalSearchService, MVCCaseManagement.Services.AI.LegalSearchService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IUnifiedLegalResearchService, MVCCaseManagement.Services.AI.UnifiedLegalResearchService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.ICaseContextBuilder, MVCCaseManagement.Services.AI.CaseContextBuilder>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IPromptManagementService, MVCCaseManagement.Services.AI.PromptManagementService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IAIAuditService, MVCCaseManagement.Services.AI.AIAuditService>();
builder.Services.AddScoped<MVCCaseManagement.Services.AI.IPostGenerationVerifier, MVCCaseManagement.Services.AI.PostGenerationVerifier>();


// Add SMS Service
builder.Services.AddHttpClient<ISMSService, SMSService>();

// Add TR-18 API Service with standard TLS verification
builder.Services.AddHttpClient<ITR18Service, TR18Service>();

// Add NAPIX quota tracker (singleton — shared across all requests so the call counter is accurate)
builder.Services.AddSingleton<MVCCaseManagement.Utils.NapixQuotaService>();

// Add eCourts NAPIX Service with modern TLS 1.2 / TLS 1.3 protocol enforcement
builder.Services.AddHttpClient<IECourtsNapixService, ECourtsNapixService>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        },
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

// Add NAPIX Sync Engine and Background Hosted Service
builder.Services.AddSingleton<MVCCaseManagement.Utils.NapixSyncEngine>();
builder.Services.AddHostedService<MVCCaseManagement.Services.NapixBackgroundSyncService>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });

// Enterprise Role-Based & Claims-Based Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MasterDataAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.HasClaim(c => c.Type == "DivisionID" && (c.Value == "5" || c.Value == "0"))));

    options.AddPolicy("CentralOfficeAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("CO") ||
            context.User.IsInRole("CLO") ||
            context.User.IsInRole("MD") ||
            context.User.IsInRole("LO") ||
            context.User.IsInRole("Dy CLO") ||
            context.User.IsInRole("DyCLO") ||
            context.User.HasClaim(c => c.Type == "DivisionID" && (c.Value == "5" || c.Value == "0"))));

    options.AddPolicy("CLOAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("CLO")));

    options.AddPolicy("MDAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("MD")));

    // Nyaya Patha AI Assistant Access Policy:
    // Open to all authenticated NWKRTC users across all Divisions and Central Office ($0 Local AI Cost)
    options.AddPolicy("NyayaPathaAIAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

// Run Versioned Database Migrations with distributed locking (sp_getapplock)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try 
    {
        var dbHelper = scope.ServiceProvider.GetRequiredService<DBHelper>();
        DatabaseMigrationRunner.RunMigrations(dbHelper, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration runner failed on startup: {Message}", ex.Message);
    }
}

// Request Correlation ID Middleware for distributed tracing and observability
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var logDir = Path.Combine(app.Environment.ContentRootPath, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "fatal-error.txt");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow}] {ex}\n\n");
            throw;
        }
    });

    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseResponseCompression();

// Serve standard public static assets (CSS, JS, images, favicon) with client-side caching headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static assets for 7 days to eliminate redundant downloads and maximize page load speeds
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800");
    }
});

app.UseRouting();

// Production-Grade Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self' https: http: data:; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https: http:; " +
        "style-src 'self' 'unsafe-inline' https: http:; " +
        "font-src 'self' data: https: http:; " +
        "img-src 'self' data: https: http: blob:; " +
        "connect-src 'self' https: http:; " +
        "frame-ancestors 'self';";
    await next();
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// SRE Health Check Endpoints
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
