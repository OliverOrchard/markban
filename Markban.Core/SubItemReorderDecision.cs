/// <summary>Outcome of applying the <c>subItemReorder</c> setting to a reorder that touches sub-items.</summary>
public enum SubItemReorderDecision
{
    Invalid,
    Blocked,
    NeedsConfirmation,
    Proceed
}
