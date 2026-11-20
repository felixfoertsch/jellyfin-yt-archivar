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
    /// Metadata provider for the configured archive library.
    /// </summary>
    public class SeriesMetadataProvider : IRemoteMetadataProvider<Series, SeriesInfo>
    {
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesMetadataProvider"/> class.
        /// </summary>
        public SeriesMetadataProvider()
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
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();
            if (!Utils.IsArchivePath(info.Path, isFolder: true))
            {
                return result;
            }

            var serverApi = ServerApi.GetInstance();
            var channelId = Utils.GetChannelNameFromPath(info.Path);
            var channel = await serverApi.GetChannel(channelId, cancellationToken).ConfigureAwait(true);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting metadata for channel: {Title} ({Id})", channel?.Name, channelId);
            }

            if (channel != null)
            {
                var peopleInfo = new List<PersonInfo>();
                PeopleHelper.AddPerson(peopleInfo, new PersonInfo
                {
                    Name = channel.Name,
                    Type = Data.Enums.PersonKind.Actor,
                });
                result.HasMetadata = true;
                result.Item = channel.ToSeries();
                if (channel.Tags == null)
                {
                    var existing = Plugin.Instance!.LibraryManager.FindByPath(info.Path, false);
                    if (existing == null)
                    {
                        return new MetadataResult<Series>();
                    }

                    result.Item.Tags = existing.Tags;
                }

                result.Provider = Name;
                result.People = peopleInfo;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();
            if (!Utils.IsArchivePath(searchInfo.Path, isFolder: true))
            {
                return results;
            }

            var serverApi = ServerApi.GetInstance();
            var channelId = Utils.GetChannelNameFromPath(searchInfo.Path);
            var channel = await serverApi.GetChannel(channelId, cancellationToken).ConfigureAwait(true);
            if (channel != null)
            {
                var entry = channel.ToSearchResult();
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
