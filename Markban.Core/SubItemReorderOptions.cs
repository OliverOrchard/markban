/// <param name="Input">Where the confirmation answer is read from.</param>
/// <param name="InputIsInteractive">False when stdin is redirected; a prompt is then treated as "no".</param>
public sealed record SubItemReorderOptions(bool DryRun, bool AssumeYes, TextReader Input, bool InputIsInteractive);
