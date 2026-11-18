using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TubeArchivistMetadata.Configuration
{
    /// <summary>Metadata-only settings. Unknown legacy sync settings are ignored.</summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>Gets or sets the TubeArchivist library display name.</summary>
        public string CollectionTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the backend base URL.</summary>
        public string TubeArchivistUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets the API token.</summary>
        public string TubeArchivistApiKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the maximum overview length.</summary>
        public int MaxDescriptionLength { get; set; } = 500;

        /// <summary>Gets or sets the episode numbering scheme.</summary>
        public NumberingScheme EpisodeNumberingScheme { get; set; } = NumberingScheme.Default;
    }
}
