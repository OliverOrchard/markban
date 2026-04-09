using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Markban;

public static class WebServer
{
    public static void Run(string rootPath, int port = 5000, bool noOpen = false)
    {
        var url = $"http://localhost:{port}";

        // wwwroot is bundled next to the tool's DLL, not in the user's working directory
        var toolDir = AppContext.BaseDirectory;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Path.Combine(toolDir, "wwwroot")
        });
        builder.WebHost.UseUrls(url);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "no-store";
            }
        });

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
                return Results.BadRequest(new { error = "Missing 'identifier' or 'target'." });

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
                return Results.BadRequest(new { error = message });

            return Results.Ok(new { message });
        });

        Console.WriteLine($"markban board running at {url}");

        if (!noOpen)
        {
            Task.Run(async () =>
            {
                await Task.Delay(500);
                OpenBrowser(url);
            });
        }

        app.Run();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", url);
            else
                System.Diagnostics.Process.Start("xdg-open", url);
        }
        catch { /* best-effort */ }
    }

    private record MoveRequest(string Identifier, string Target);
}
