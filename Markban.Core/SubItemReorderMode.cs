/// <summary>Valid values of the top-level <c>subItemReorder</c> key in markban.json.</summary>
public static class SubItemReorderMode
{
    public const string Warn = "warn";
    public const string Block = "block";
    public const string Force = "force";

    private static readonly string[] ValidModes = [Warn, Block, Force];

    public static bool IsValid(string? value) => ValidModes.Contains(value);

    public static string InvalidValueMessage(string? value)
        => $"Invalid subItemReorder '{value}' in markban.json. Valid values: {string.Join(", ", ValidModes)}.";
}
