namespace Markban;

/// <summary>Body of POST /api/reorder. <paramref name="DetachId"/> names the dragged sub-item, if any.</summary>
internal sealed record WebReorderRequest(
    string? Lane,
    IReadOnlyList<string>? OrderedIds,
    string? DetachId = null,
    bool ConfirmDetach = false);
