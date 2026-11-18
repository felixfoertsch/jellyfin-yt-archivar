using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.YouTubeArchivar.TubeArchivist;
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
    /// Image provider which interacts with TubeArchivist library.
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
            if (!Utils.IsTubeArchivistItem(item))
            {
                return Array.Empty<RemoteImageInfo>();
            }

            var list = new List<RemoteImageInfo>();
            var taApi = TubeArchivistApi.GetInstance();
            var channelTAId = Utils.GetChannelNameFromPath(item.Path);
            var channel = await taApi.GetChannel(channelTAId, cancellationToken).ConfigureAwait(true);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting images for channel: {Title} ({Id})", channel?.Name, channelTAId);
            }

            if (channel != null && Utils.HasImageUrl(channel.ThumbUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = taApi.NormalizeImage(channel.ThumbUrl)
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.TvartUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Art,
                    Url = taApi.NormalizeImage(channel.TvartUrl)
                });

                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Backdrop,
                    Url = taApi.NormalizeImage(channel.TvartUrl)
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.BannerUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Banner,
                    Url = taApi.NormalizeImage(channel.BannerUrl)
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
                    throw new HttpRequestException("TubeArchivist returned an empty image URL.");
                }

                return await TubeArchivistApi.GetInstance().GetImage(url, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
