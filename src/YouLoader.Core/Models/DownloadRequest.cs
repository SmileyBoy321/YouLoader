namespace YouLoader.Core.Models;

/// <summary>What the user asked for when they pressed Download.</summary>
public sealed record DownloadRequest(
    OutputFormat Format,
    string Quality,
    bool WholePlaylist,
    bool EmbedArt,
    string OutputDir);
