// CheckLinksRoute is superseded by HealthRoute (markban health check-links).
// Kept in source for reference; not registered in CommandRouter.
public class CheckLinksRoute : CommandRoute
{
    public override HelpEntry Help => new HelpEntry(
        "health check-links [--include-ideas]",
        "Find broken [slug] cross-references (use 'markban health')");

    public override bool TryRoute(string[] args, string rootPath)
    {
        return false;
    }
}
