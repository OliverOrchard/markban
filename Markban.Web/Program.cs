var rootPath = WorkItemStore.FindRoot();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/items", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    var items = WorkItemStore.LoadAll(rootPath);
    return Results.Ok(items);
});

app.MapPost("/api/move", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<MoveRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Identifier) || string.IsNullOrWhiteSpace(body.Target))
    {
        return Results.BadRequest(new { error = "Missing 'identifier' or 'target'." });
    }

    var stdout = new StringWriter();
    var originalOut = Console.Out;
    Console.SetOut(stdout);
    try
    {
        MoveCommand.Execute(rootPath, body.Identifier, body.Target);
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    var message = stdout.ToString().TrimEnd();
    if (message.StartsWith("Error:"))
    {
        return Results.BadRequest(new { error = message });
    }

    return Results.Ok(new { message });
});

app.MapGet("/api/reports/cycle-time", async (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    try
    {
        var entries = await CycleTimeCommand.ExecuteAsync(rootPath);
        return Results.Ok(entries);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Failed to compute cycle time");
    }
});

app.MapGet("/reports/cycle-time", (HttpContext context) =>
{
    context.Response.Redirect("/reports/cycle-time.html", permanent: false);
    return Results.Empty;
});

app.Run();

internal record MoveRequest(string Identifier, string Target);
