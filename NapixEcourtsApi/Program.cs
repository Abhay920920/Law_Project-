using NapixEcourtsApi.Configuration;
using NapixEcourtsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<NapixOptions>(
    builder.Configuration.GetSection(NapixOptions.SectionName));

// --- Core services ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "NYAYA PATHA — NAPIX eCourts Integration",
        Version = "v1",
        Description = "Wraps NAPIX/eCourts High Court & District Court Case Status APIs (CNR search, " +
                      "case details, hearings, orders, judgments, show-business) behind a clean API."
    });
});

// --- NAPIX pipeline ---
Func<HttpMessageHandler> createResilientHandler = () => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
    EnableMultipleHttp2Connections = false,
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
    }
};

builder.Services.AddHttpClient(nameof(NapixAuthService))
    .ConfigurePrimaryHttpMessageHandler(createResilientHandler);

builder.Services.AddSingleton<INapixAuthService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NapixOptions>>();
    return new NapixAuthService(factory.CreateClient(nameof(NapixAuthService)), options);
});

builder.Services.AddScoped<INapixCryptoService, NapixCryptoService>();
builder.Services.AddHttpClient<INapixEcourtsClient, NapixEcourtsClient>()
    .ConfigurePrimaryHttpMessageHandler(createResilientHandler);

var app = builder.Build();

app.UseDeveloperExceptionPage();

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
