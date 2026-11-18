namespace Jellyfin.Plugin.YouTubeArchivar.Configuration;

/// <summary>
/// The NumberingScheme.
/// </summary>
public enum NumberingScheme
{
    /// <summary>
    /// Default (no numbering).
    /// </summary>
    Default,

    /// <summary>
    /// YYYYMMDD (e.g. 20250804 for August 4th, 2025).
    /// </summary>
    YYYYMMDD,
}
