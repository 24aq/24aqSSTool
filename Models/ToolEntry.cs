namespace TwentyFourAqSSTool.Models;

public sealed class ToolEntry
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Others";
    public string SourceType { get; set; } = "GH";
    public string DownloadUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Sha256 { get; set; }
    public string? HomePage { get; set; }
}