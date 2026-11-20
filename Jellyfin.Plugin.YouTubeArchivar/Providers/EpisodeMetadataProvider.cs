using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
    /// Metadata provider for the configured archive library.
    /// </summary>
    public class EpisodeMetadataProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeMetadataProvider"/> class.
        /// </summary>
        public EpisodeMetadataProvider()
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
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();
            if (!Utils.IsArchivePath(info.Path, isFolder: false))
            {
                return result;
            }

            var serverApi = ServerApi.GetInstance();
            var videoId = Utils.GetVideoNameFromPath(info.Path);
            var video = await serverApi.GetVideo(videoId, cancellationToken).ConfigureAwait(true);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting metadata for video: {Title} ({Id})", video?.Title, videoId);
            }

            if (video != null)
            {
                var peopleInfo = new List<PersonInfo>();
                PeopleHelper.AddPerson(peopleInfo, new PersonInfo
                {
                    Name = video.Channel.Name,
                    Type = Data.Enums.PersonKind.Actor,
                });
                result.HasMetadata = true;
                result.Item = video.ToEpisode();
                result.Item.Path = info.Path;
                result.Provider = Name;
                result.People = peopleInfo;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();
            if (!Utils.IsArchivePath(searchInfo.Path, isFolder: false))
            {
                return results;
            }

            var serverApi = ServerApi.GetInstance();
            var videoId = Utils.GetVideoNameFromPath(searchInfo.Path);
            var video = await serverApi.GetVideo(videoId, cancellationToken).ConfigureAwait(true);
            if (video != null)
            {
                var entry = video.ToSearchResult();
                entry.ImageUrl = Utils.HasImageUrl(entry.ImageUrl) ? serverApi.NormalizeImage(entry.ImageUrl) : null;
                results.Add(entry);
            }

            return results;
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
                return await ServerApi.GetInstance().GetImage(url, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
