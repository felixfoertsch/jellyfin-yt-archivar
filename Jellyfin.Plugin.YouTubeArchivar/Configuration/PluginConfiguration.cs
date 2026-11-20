using System.Text.Json.Serialization;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.YouTubeArchivar.Configuration
{
    /// <summary>Metadata-only settings. Unknown legacy sync settings are ignored.</summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        private string? _serverUrl;
        private string? _apiKey;

        /// <summary>Gets or sets the archive library display name.</summary>
        public string CollectionTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the backend base URL.</summary>
        public string ServerUrl
        {
            get => _serverUrl ?? string.Empty;
            set => _serverUrl = value;
        }

        /// <summary>Gets or sets the API token.</summary>
        public string ApiKey
        {
            get => _apiKey ?? string.Empty;
            set => _apiKey = value;
        }

        // Read the former element names in saved Jellyfin XML. Null getters omit the aliases on save.
        // An explicitly set current value takes precedence, even when it is empty.

        /// <summary>Gets or sets the legacy XML alias for the server URL.</summary>
        [JsonIgnore]
        [XmlElement("TubeArchivistUrl")]
        public string? LegacyServerUrl
        {
            get => null;
            set => _serverUrl ??= value;
        }

        /// <summary>Gets or sets the legacy XML alias for the API key.</summary>
        [JsonIgnore]
        [XmlElement("TubeArchivistApiKey")]
        public string? LegacyApiKey
        {
            get => null;
            set => _apiKey ??= value;
        }

        /// <summary>Gets or sets the maximum overview length.</summary>
        public int MaxDescriptionLength { get; set; } = 500;

        /// <summary>Gets or sets the episode numbering scheme.</summary>
        public NumberingScheme EpisodeNumberingScheme { get; set; } = NumberingScheme.Default;
    }
}
