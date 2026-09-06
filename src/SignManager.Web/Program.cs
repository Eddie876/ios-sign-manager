using SignManager.Infrastructure.Persistence;
using SignManager.Signing.Ipa;
using SignManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.Configure<WebUiOptions>(builder.Configuration.GetSection("WebUi"));

builder.Services.AddSingleton<AppConfigStore>();
builder.Services.AddSingleton<AppStateStore>();
builder.Services.AddSingleton<WebSettingsStore>();
builder.Services.AddSingleton<ShortcutTokenStore>();
builder.Services.AddSingleton<IpaPreflightService>();
builder.Services.AddSingleton<SourceIpaManager>();
builder.Services.AddSingleton<WebAppService>();
builder.Services.AddSingleton<ShortcutApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/api/shortcut/refresh-plan", async (
    HttpContext httpContext,
    ShortcutApiService shortcutService,
    CancellationToken cancellationToken) =>
{
    var token = ExtractBearerToken(httpContext.Request.Headers.Authorization);
    if (!await shortcutService.ValidateTokenAsync(token, cancellationToken))
    {
        return Results.Unauthorized();
    }

    try
    {
        var response = await shortcutService.GetRefreshPlanAsync(DateTimeOffset.UtcNow, cancellationToken);
        return Results.Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "Shortcut API configuration error",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/shortcut/prompted", async (
    HttpContext httpContext,
    ShortcutPromptedRequest? request,
    ShortcutApiService shortcutService,
    CancellationToken cancellationToken) =>
{
    var token = ExtractBearerToken(httpContext.Request.Headers.Authorization);
    if (!await shortcutService.ValidateTokenAsync(token, cancellationToken))
    {
        return Results.Unauthorized();
    }

    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Request body is required."],
        });
    }

    if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.BuildId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.AppId)] = ["AppId is required."],
            [nameof(request.BuildId)] = ["BuildId is required."],
        });
    }

    var updated = await shortcutService.MarkPromptedAsync(
        request.AppId,
        request.BuildId,
        DateTimeOffset.UtcNow,
        cancellationToken);

    return updated ? Results.Ok() : Results.NotFound();
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static string? ExtractBearerToken(string? authorization)
{
    if (string.IsNullOrWhiteSpace(authorization))
    {
        return null;
    }

    const string prefix = "Bearer ";
    return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? authorization[prefix.Length..].Trim()
        : null;
}
