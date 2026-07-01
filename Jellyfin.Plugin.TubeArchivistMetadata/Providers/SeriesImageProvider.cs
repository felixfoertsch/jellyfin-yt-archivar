using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TubeArchivistMetadata.TubeArchivist;
using Jellyfin.Plugin.TubeArchivistMetadata.Utilities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TubeArchivistMetadata.Providers
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
        public string Name => "TubeArchivist";

        /// <inheritdoc />
        public bool Supports(BaseItem item) => item is Series && Utils.IsTubeArchivistItem(item);

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary, ImageType.Art, ImageType.Banner, ImageType.Backdrop };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            var taApi = TubeArchivistApi.GetInstance();
            var channelTAId = Utils.GetChannelNameFromPath(item.Path);
            var channel = await taApi.GetChannel(channelTAId).ConfigureAwait(true);
            _logger.LogDebug("{Message}", string.Format(CultureInfo.CurrentCulture, "Getting images for channel: {0} ({1})", channel?.Name, channelTAId));
            _logger.LogDebug("{Message}", "Thumb URI: " + channel?.ThumbUrl);

            if (channel != null && Utils.HasImageUrl(channel.ThumbUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = channel.ThumbUrl
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.TvartUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Art,
                    Url = channel.TvartUrl
                });

                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Backdrop,
                    Url = channel.TvartUrl
                });
            }

            if (channel != null && Utils.HasImageUrl(channel.BannerUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Banner,
                    Url = channel.BannerUrl
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

                return await Plugin.Instance.HttpClient.GetAsync(new Uri(Utils.SanitizeUrl(Plugin.Instance.Configuration.TubeArchivistUrl + url).TrimEnd('/')), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
