namespace Markban;

internal static class WebReorderHandler
{
    private const string ConfirmationRequiredMessage = "Detaching a sub-item requires confirmation (confirmDetach).";

    public static WebReorderResult Execute(string rootPath, WebReorderRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Lane) || request.OrderedIds is not { Count: > 0 })
        {
            return new WebReorderResult(false, "Missing 'lane' or 'orderedIds'.");
        }

        var rejection = CheckSubItemPolicy(WorkItemStore.LoadSettings(rootPath).SubItemReorder, request);
        if (rejection is not null)
        {
            return rejection;
        }

        var detachIds = string.IsNullOrEmpty(request.DetachId) ? [] : new[] { request.DetachId };
        var result = DetachReorderCommand.Execute(rootPath, request.Lane, request.OrderedIds, detachIds);
        return new WebReorderResult(result.IsSuccess, result.Message);
    }

    private static WebReorderResult? CheckSubItemPolicy(string mode, WebReorderRequest request)
    {
        var decision = SubItemReorderPolicy.Decide(mode);
        if (decision == SubItemReorderDecision.Invalid)
        {
            return new WebReorderResult(false, SubItemReorderMode.InvalidValueMessage(mode));
        }

        if (string.IsNullOrEmpty(request.DetachId))
        {
            return null;
        }

        return decision switch
        {
            SubItemReorderDecision.Blocked => new WebReorderResult(false, SubItemReorderPolicy.WebBlockedMessage),
            SubItemReorderDecision.NeedsConfirmation when !request.ConfirmDetach => new WebReorderResult(false, ConfirmationRequiredMessage),
            _ => null
        };
    }
}
