public static class ProgressCommand
{
    public static void Execute(string rootPath, string identifier, bool dryRun = false)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var allLanes = WorkItemStore.LoadConfig(rootPath);
        var flow = allLanes.Where(l => l.Pickable).ToList();

        var currentIdx = flow.FindIndex(l => l.Name.Equals(item.Status, StringComparison.OrdinalIgnoreCase));
        if (currentIdx < 0)
        {
            Console.Error.WriteLine($"Error: Item '{identifier}' is in lane '{item.Status}' which is not on the progress path. Use 'markban move' instead.");
            return;
        }

        var doneLane = flow.FirstOrDefault(l => l.Type == "done");
        if (doneLane != null && item.Status.Equals(doneLane.Name, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Item '{identifier}' is already in the done lane ({doneLane.Name}).");
            return;
        }

        var nextLane = flow.ElementAtOrDefault(currentIdx + 1);
        if (nextLane == null)
        {
            Console.WriteLine($"Item '{identifier}' is already at the last lane in the workflow.");
            return;
        }

        if (dryRun)
        {
            Console.WriteLine($"Would move '{item.Id}' ({item.Slug}) from '{item.Status}' -> '{nextLane.Name}'");
            return;
        }

        MoveCommand.Execute(rootPath, identifier, nextLane.Name);
    }
}
