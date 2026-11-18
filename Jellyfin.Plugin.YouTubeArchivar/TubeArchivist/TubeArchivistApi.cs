using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.YouTubeArchivar.TubeArchivist
{
    /// <summary>Read-only client with a captured destination and credential.</summary>
    public class TubeArchivistApi
    {
        private readonly HttpClient _client;
        private readonly Uri _base;
        private readonly string _token;
        private readonly string _configuredUrl;

        /// <summary>Initializes a new instance of the <see cref="TubeArchivistApi"/> class.</summary>
        /// <param name="httpClient">Redirect-disabled HTTP client.</param>
        public TubeArchivistApi(HttpClient httpClient)
        {
            _client = httpClient;
            var config = Plugin.Instance!.Configuration;
            _configuredUrl = config.TubeArchivistUrl;
            var value = _configuredUrl.Trim();
            if (!value.Contains("://", StringComparison.Ordinal))
            {
                value = "http://" + value;
            }

            if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var url)
                || (url.Scheme != "http" && url.Scheme != "https") || !string.IsNullOrEmpty(url.UserInfo)
                || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            {
                throw new DataException("Configure an HTTP(S) backend URL without credentials, query or fragment.");
            }

            _base = url;
            _token = config.TubeArchivistApiKey;
            if (string.IsNullOrWhiteSpace(_token))
            {
                throw new DataException("Configure a TubeArchivist API token.");
            }
        }

        /// <summary>Creates a client for the current settings.</summary>
        /// <returns>The client.</returns>
        public static TubeArchivistApi GetInstance() => new TubeArchivistApi(Plugin.Instance!.HttpClient);

        private void EnsureCurrent()
        {
            var config = Plugin.Instance!.Configuration;
            if (config.TubeArchivistUrl != _configuredUrl || config.TubeArchivistApiKey != _token)
            {
                throw new DataException("Connection changed; response discarded.");
            }
        }

        private async Task<HttpResponseMessage> Send(Uri url, CancellationToken cancellationToken)
        {
            if (url.Scheme != _base.Scheme || url.Host != _base.Host || url.Port != _base.Port
                || !url.AbsolutePath.StartsWith(_base.AbsolutePath, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            {
                throw new DataException("Refusing credentials outside the configured backend base.");
            }

            EnsureCurrent();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", _token);
            var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureCurrent();
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        /// <summary>Reads protected artwork within the backend base.</summary>
        /// <param name="path">Relative or same-origin image URL.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The authenticated response.</returns>
        public Task<HttpResponseMessage> GetImage(string path, CancellationToken cancellationToken)
        {
            return Send(new Uri(NormalizeImage(path)), cancellationToken);
        }

        /// <summary>Validates and resolves only maintained backend thumbnail routes.</summary>
        /// <param name="path">Backend artwork reference.</param>
        /// <returns>The guarded absolute URL.</returns>
        public string NormalizeImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains('%', StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal)
                || path.Contains("..", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                throw new DataException("Ambiguous artwork path.");
            }

            string relative;
            if (path.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            {
                if (absolute.Scheme != _base.Scheme || absolute.Authority != _base.Authority
                    || !absolute.AbsolutePath.StartsWith(_base.AbsolutePath, StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(absolute.UserInfo) || !string.IsNullOrEmpty(absolute.Query) || !string.IsNullOrEmpty(absolute.Fragment))
                {
                    throw new DataException("External artwork rejected.");
                }

                relative = absolute.AbsolutePath.Substring(_base.AbsolutePath.Length);
            }
            else
            {
                relative = path.StartsWith(_base.AbsolutePath, StringComparison.Ordinal)
                    ? path.Substring(_base.AbsolutePath.Length) : path.TrimStart('/');
            }

            if (!Regex.IsMatch(relative, "\\Amedia/thumb/(video/[A-Za-z0-9_-]{11}|channel/UC[A-Za-z0-9_-]{22}/(thumb|banner|tvart))\\.jpg\\z"))
            {
                throw new DataException("Unknown artwork route.");
            }

            return new Uri(_base, relative).AbsoluteUri;
        }

        private async Task<T?> Read<T>(string path, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await Send(new Uri(_base, path), cancellationToken).ConfigureAwait(false);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var document = JObject.Parse(raw);
                var description = document[typeof(T) == typeof(Channel) ? "channel_description" : "description"];
                var tags = document[typeof(T) == typeof(Channel) ? "channel_tags" : "tags"];
                if (description == null || (description.Type != JTokenType.String && description.Type != JTokenType.Null)
                    || (tags == null ? typeof(T) == typeof(Video) : tags.Type != JTokenType.Array || tags.Any(tag => tag.Type != JTokenType.String)))
                {
                    throw new DataException("Incomplete replacement metadata.");
                }

                var result = document.ToObject<T>();
                EnsureCurrent();
                return result ?? throw new DataException("Missing metadata response.");
            }
            catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }
        }

        /// <summary>Reads channel metadata.</summary>
        /// <param name="channelId">YouTube channel ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metadata, or null for a missing channel.</returns>
        public async Task<Channel?> GetChannel(string channelId, CancellationToken cancellationToken = default)
        {
            if (!Regex.IsMatch(channelId, "^UC[A-Za-z0-9_-]{22}$"))
            {
                throw new DataException("Invalid channel ID.");
            }

            var result = await Read<Channel>("api/channel/" + channelId + "/", cancellationToken).ConfigureAwait(false);
            if (result != null && (result.Id != channelId || string.IsNullOrWhiteSpace(result.Name)))
            {
                throw new DataException("Invalid channel metadata.");
            }

            return result;
        }

        /// <summary>Reads video metadata without playback authority.</summary>
        /// <param name="videoId">YouTube video ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Metadata, or null for a missing video.</returns>
        public async Task<Video?> GetVideo(string videoId, CancellationToken cancellationToken = default)
        {
            if (!Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$"))
            {
                throw new DataException("Invalid video ID.");
            }

            var result = await Read<Video>("api/video/" + videoId + "/", cancellationToken).ConfigureAwait(false);
            if (result != null && (result.YoutubeId != videoId || string.IsNullOrWhiteSpace(result.Title)
                || result.Published == default || result.Channel == null || string.IsNullOrWhiteSpace(result.Channel.Name) || result.Tags == null))
            {
                throw new DataException("Invalid video metadata.");
            }

            return result;
        }
    }
}
