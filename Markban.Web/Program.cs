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

app.Run();

internal record MoveRequest(string Identifier, string Target);
