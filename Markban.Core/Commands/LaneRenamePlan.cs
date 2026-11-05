internal record LaneRenamePlan(
    string OldName,
    string NewName,
    string SourcePath,
    string TargetPath,
    string ConfigPath,
    string OriginalConfig,
    string UpdatedConfig);
