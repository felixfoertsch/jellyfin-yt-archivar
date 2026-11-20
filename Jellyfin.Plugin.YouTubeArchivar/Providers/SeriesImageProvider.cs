using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.YouTubeArchivar.Server;
using Jellyfin.Plugin.YouTubeArchivar.Utilities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YouTubeArchivar.Providers
{
    /// <summary>
    /// Image provider for the configured archive library.
    /// </summary>
    public class SeriesImageProvider : IRemoteImageProvider
    {
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesImageProvider"/> class.
        /// </summary>
        public SeriesImageProvider()
        {
            if (Plugin.Instance == null)
            {
                throw new DataException("Uninitialized plugin!");
            }
            else
            {
                _logger = Plugin.Instance.Logger;
            }
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string Name => Constants.ProviderName;

        /// <inheritdoc />
        public bool Supports(BaseItem item) => item is Series;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary, ImageType.Art, ImageType.Banner, ImageType.Backdrop };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            // Jellyfin probes Supports with an unparented item when listing library options.
            if (!Utils.IsArchiveItem(item))
            {
                return Array.Empty<RemoteImageInfo>();
            }

            var list = new List<RemoteImageInfo>();
            var serverApi = ServerApi.GetInstance();
            var channelId = Utils.GetChannelNameFromPath(item.Path);
            var channel = await serverApi.GetChannel(channelId, cancellationToken).ConfigureAwait(true);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting images for channel: {Title} ({Id})", channel?.Name, channelId);
            }

            if (channel != null && Utils.HasImageUrl(channel.ThumbUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = serverApi.NormalizeImage(channel.ThumbUrl)
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.TvartUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Art,
                    Url = serverApi.NormalizeImage(channel.TvartUrl)
                });

                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Backdrop,
                    Url = serverApi.NormalizeImage(channel.TvartUrl)
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.BannerUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Banner,
                    Url = serverApi.NormalizeImage(channel.BannerUrl)
                });
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            if (Plugin.Instance == null)
            {
                throw new DataException("Uninitialized plugin!");
            }
            else
            {
                if (!Utils.HasImageUrl(url))
                {
                    throw new HttpRequestException("The server returned an empty image URL.");
                }

                return await ServerApi.GetInstance().GetImage(url, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
