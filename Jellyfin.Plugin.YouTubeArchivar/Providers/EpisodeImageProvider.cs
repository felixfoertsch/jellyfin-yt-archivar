using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
    public class EpisodeImageProvider : IRemoteImageProvider
    {
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeImageProvider"/> class.
        /// </summary>
        public EpisodeImageProvider()
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
        public bool Supports(BaseItem item) => item is Episode;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
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
            var videoTAId = Utils.GetVideoNameFromPath(item.Path);
            var video = await taApi.GetVideo(videoTAId, cancellationToken).ConfigureAwait(true);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting images for video: {Title} ({Id})", video?.Title, videoTAId);
            }

            if (video != null && Utils.HasImageUrl(video.VidThumbUrl))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = taApi.NormalizeImage(video.VidThumbUrl)
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
