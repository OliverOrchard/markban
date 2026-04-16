using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Markban;

public static class WebServer
{
    public static void Run(string rootPath, int port = 5000, bool noOpen = false)
    {
        var url = $"http://localhost:{port}";
        var configDir = Path.GetDirectoryName(rootPath) ?? rootPath;
        IReadOnlyList<BoardEntry> boards;

        try
        {
            boards = WorkItemStore.LoadBoards(configDir);
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return;
        }

        var toolDir = AppContext.BaseDirectory;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Path.Combine(toolDir, "wwwroot")
        });
        builder.WebHost.UseUrls(url);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "no-store";
            }
        });

        app.MapGet("/api/boards", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            var result = boards.Select(board => new { name = board.Name, key = board.Key }).ToArray();
            return Results.Ok(result);
        });

        app.MapGet("/api/items", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            var resolvedRoot = ResolveBoard(context, boards, rootPath, out var error);
            if (resolvedRoot is null)
            {
                return Results.BadRequest(new { error });
            }

            var items = WorkItemStore.LoadAll(resolvedRoot);
            return Results.Ok(items);
        });

        app.MapGet("/api/lanes", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            var resolvedRoot = ResolveBoard(context, boards, rootPath, out var error);
            if (resolvedRoot is null)
            {
                return Results.BadRequest(new { error });
            }

            var laneNames = WorkItemStore.LoadConfig(resolvedRoot).Select(lane => lane.Name).ToArray();
            return Results.Ok(laneNames);
        });

        app.MapPost("/api/move", async (HttpContext context) =>
        {
            var body = await context.Request.ReadFromJsonAsync<MoveRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Identifier) || string.IsNullOrWhiteSpace(body.Target))
            {
                return Results.BadRequest(new { error = "Missing 'identifier' or 'target'." });
            }

            var resolvedRoot = ResolveBoard(context, boards, rootPath, out var boardError);
            if (resolvedRoot is null)
            {
                return Results.BadRequest(new { error = boardError });
            }

            var stdout = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(stdout);
            try
            {
                MoveCommand.Execute(resolvedRoot, body.Identifier, body.Target);
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
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", url);
            }
            else
            {
                System.Diagnostics.Process.Start("xdg-open", url);
            }
        }
        catch
        {
        }
    }

    private static string? ResolveBoard(
        HttpContext context,
        IReadOnlyList<BoardEntry> boards,
        string defaultRoot,
        out string? error)
    {
        var boardKey = context.Request.Query["board"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(boardKey))
        {
            error = null;
            return defaultRoot;
        }

        var entry = boards.FirstOrDefault(board => string.Equals(board.Key, boardKey, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            error = $"Unknown board '{boardKey}'.";
            return null;
        }

        try
        {
            error = null;
            return WorkItemStore.ResolveConfiguredBoardRoot(entry.ResolvedPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            error = ex.Message;
            return null;
        }
        catch (InvalidDataException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private record MoveRequest(string Identifier, string Target);
}
