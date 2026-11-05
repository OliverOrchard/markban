using System.Text.RegularExpressions;

/// <summary>Single source of truth for the <c>subItemReorder</c> policy, used by the CLI and the web handler.</summary>
public static class SubItemReorderPolicy
{
    public const string WebBlockedMessage = "Sub-items stay with their parent. Change subItemReorder to reorder.";
    public const string CliBlockedMessage =
        "Sub-items stay with their parent. Set subItemReorder to \"warn\" or \"force\" in markban.json to reorder them.";

    private static readonly Regex SubItemIdPattern = new(@"^\d+[a-z]$", RegexOptions.Compiled);

    public static bool IsSubItemId(string? id) => id is not null && SubItemIdPattern.IsMatch(id);

    public static SubItemReorderDecision Decide(string? mode) => mode switch
    {
        SubItemReorderMode.Block => SubItemReorderDecision.Blocked,
        SubItemReorderMode.Warn => SubItemReorderDecision.NeedsConfirmation,
        SubItemReorderMode.Force => SubItemReorderDecision.Proceed,
        _ => SubItemReorderDecision.Invalid
    };
}
