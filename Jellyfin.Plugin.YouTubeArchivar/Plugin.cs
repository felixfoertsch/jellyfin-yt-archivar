using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YouTubeArchivar
{
    /// <summary>Metadata and authenticated artwork only.</summary>
    public class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
    {
        /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
        /// <param name="applicationPaths">Application paths.</param>
        /// <param name="xmlSerializer">Configuration serializer.</param>
        /// <param name="logger">Plugin logger.</param>
        /// <param name="libraryManager">Library lookup for metadata scoping.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger, ILibraryManager libraryManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            Logger = logger;
            LibraryManager = libraryManager;
            HttpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true }) { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>Gets the library lookup.</summary>
        public ILibraryManager LibraryManager { get; }

        /// <inheritdoc />
        public override string Name => Constants.PluginName;

        /// <inheritdoc />
        public override Guid Id => Guid.Parse(Constants.PluginGuid);

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Gets the logger instance used globally by the plugin.
        /// </summary>
        public ILogger<Plugin> Logger { get; }

        /// <summary>
        /// Gets the HTTP client used globally by the plugin.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
