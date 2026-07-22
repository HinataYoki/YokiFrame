namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 控制台的一条有界日志记录。</summary>
public sealed record TableKitConsoleEntryViewModel(string Time, string Level, string Message);
