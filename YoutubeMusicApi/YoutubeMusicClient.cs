﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeMusicApi.Logging;
using YoutubeMusicApi.Models;
using YoutubeMusicApi.Models.Search;

namespace YoutubeMusicApi
{
    public class YoutubeMusicClient
    {
        public static string BaseUrl = "https://music.youtube.com/youtubei/v1";
        public static string Params = "?alt=json&key=AIzaSyC9XL3ZjWddXya6X74dJoCTL-WEYFDNX30";

        public static JObject DefaultBody = JObject.FromObject(new
        {
            context = new
            {
                capabilities = new { },
                client = new
                {
                    clientName = "WEB_REMIX",
                    clientVersion = "0.1",
                    experimentIds = new List<string>(),
                    experimentsToken = "",
                    gl = "DE",
                    hl = "en",
                    locationInfo = new
                    {
                        locationPermissionAuthorizationStatus = "LOCATION_PERMISSION_AUTHORIZATION_STATUS_UNSUPPORTED"
                    },
                    musicAppInfo = new
                    {
                        musicActivityMasterSwitch = "MUSIC_ACTIVITY_MASTER_SWITCH_INDETERMINATE",
                        musicLocationMasterSwitch = "MUSIC_LOCATION_MASTER_SWITCH_INDETERMINATE",
                        pwaInstallabilityStatus = "PWA_INSTALLABILITY_STATUS_UNKNOWN"
                    },
                    utcOffsetMinutes = 60
                },
                request = new
                {
                    internalExperimentFlags = new JArray( new JObject[]
                    {
                        JObject.FromObject(new
                        {
                            key = "force_music_enable_outertube_tastebuilder_browse",
                            value = "true"
                        }),
                        JObject.FromObject(new
                        {
                            key = "force_music_enable_outertube_playlist_detail_browse",
                            value = "true"
                        }),
                        JObject.FromObject(new
                        {
                            key = "force_music_enable_outertube_search_suggestions",
                            value = "true"
                        }),
                    }),
                    sessionIndex = new { }
                },
                user = new
                {
                    enableSafetyMode = false
                }
            }
        });

        public AuthHeaders AuthHeaders { get; private set; }
        public ILogger Logger { get; set; }

        public YoutubeMusicClient(ILogger logger = null)
        {
            // init to default values w/o a cookie
            AuthHeaders = new AuthHeaders();
            Logger = logger;
        }

        #region Authentication

        public async Task<bool> LoginWithAuthJsonFile(string filePath)
        {
            using (StreamReader reader = File.OpenText(filePath))
            {
                string contents = await reader.ReadToEndAsync();
                return  LoginWithAuthHeaderString(contents);
            }
        }

        public bool LoginWithAuthHeaderString(string headersJson)
        {
            AuthHeaders = JsonConvert.DeserializeObject<AuthHeaders>(headersJson);

            return !string.IsNullOrEmpty(AuthHeaders.Cookie);
        }

        public bool LoginWithCookie(string cookie)
        {
            AuthHeaders = new AuthHeaders
            {
                Cookie = cookie
            };

            return true;
        }

        public bool IsAuthed()
        {
            return AuthHeaders != null && !string.IsNullOrEmpty(AuthHeaders.Cookie);
        }

        #endregion

        #region Artist
        public async Task<JObject> GetArtist(string id)
        {
            string url = GetYTMUrl("browse");

            var data = PrepareBrowse("ARTIST", id);

            return await Post<JObject>(url, data);
        }

        #endregion

        #region Playlists

        public async Task<JObject> GetLikedPlaylists()
        {
            string url = GetYTMUrl("browse");
            var data = JObject.FromObject(new
            {
                browseId = "FEmusic_liked_playlists"
            });

            return await AuthedPost<JObject>(url, data);
        }

        public async Task<JObject> GetPlaylist(string id)
        {
            string url = GetYTMUrl("browse");
            var data = PrepareBrowse("PLAYLIST", id);
            return await AuthedPost<JObject>(url, data);
        }

        public async Task<JObject> CreatePlaylist(string title, string description, string privacyStatus, List<string> videoIds = null, string sourcePlaylist = null)
        {
            string url = GetYTMUrl("playlist/create");
            var data = JObject.FromObject(new
            {
                title = title,
                description = description,
                privacyStatus = privacyStatus,
                // videoIds = videoIds,
                // sourcePlaylist = sourcePlaylist
            });
            return await AuthedPost<JObject>(url, data);
        }

        public async Task<JObject> DeletePlaylist(string playlistId)
        {
            string url = GetYTMUrl("playlist/delete");
            var data = JObject.FromObject(new
            {
                playlistId = playlistId
            });
            return await AuthedPost<JObject>(url, data);
        }

        public async Task<JObject> RemovePlaylistItems(string playlistId,  List<object> videos)
        {
            string url = GetYTMUrl("browse/edit_playlist");
            var data = JObject.FromObject(new
            {

            });
            return await AuthedPost<JObject>(url, data);
        }

        #endregion

        #region Search

        public async Task<SearchResult> Search(string search, SearchResultType filter = SearchResultType.All, bool authRequired = false)
        {
            string url = GetYTMUrl("search");

            JObject data = JObject.FromObject(new 
            { 
                query = search
            });

            if (filter != SearchResultType.All)
            {
                string parameters = GetSearchParamStringFromFilter(filter);
                data.Add("params", parameters);
            }

            if (filter == SearchResultType.Upload)
            {
                // if we're explicitly looking for uploads... then make sure auth is required
                // if you call this with just search all and no auth, then the results
                // won't include uploads
                authRequired = true;
            }

            GeneratedSearchResult result = await Post<GeneratedSearchResult>(url, data, authRequired: authRequired);

            SearchResult results = SearchResult.ParseResultListFromGenerated(result, filter);
            
            return results;
        }

        private string GetSearchParamStringFromFilter(SearchResultType filter)
        {
            string param1 = "Eg-KAQwIA";
            string param3 = "MABqChAEEAMQCRAFEAo%3D";
            string parameters = "";

            if (filter == SearchResultType.Upload)
            {
                parameters = "agIYAw%3D%3D";
            }
            else
            {
                string param2 = "";
                switch (filter)
                {
                    case SearchResultType.Album:
                        param2 = "BAAGAEgACgA";
                        break;
                    case SearchResultType.Artist:
                        param2 = "BAAGAAgASgA";
                        break;
                    case SearchResultType.Playlist:
                        param2 = "BAAGAAgACgB";
                        break;
                    case SearchResultType.Song:
                        param2 = "RAAGAAgACgA"; // not sure if this is right, python code has this under else case but not explicitly for songs
                        break;
                    case SearchResultType.Video:
                        param2 = "BABGAAgACgA";
                        break;
                    case SearchResultType.Upload:
                        param2 = "RABGAEgASgB"; // not sure if this is right, uploads should never get here due to above if clause
                        break;
                    default:
                        throw new Exception($"Unsupported search filter type: {filter}");
                }

                parameters = param1 + param2 + param3;
            }

            return parameters;
        }
        #endregion

        #region Requests

        private async Task<T> Get<T>(string url)
        {
            HttpClient client = GetHttpClient();

            Log($"GET: {url}");
            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            Log($"\tRESPONSE: {responseString}");
            T result = JsonConvert.DeserializeObject<T>(responseString);
            return result;
        }

        private async Task<T> Post<T>(string url, JObject data,  bool authRequired = false)
        {
            HttpClient client = GetHttpClient(authRequired: authRequired);

            data.Merge(DefaultBody);
            string requestBody = data.ToString();
            HttpContent content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            Log($"POST: {url}");
            Log($"\tBODY: {requestBody}");
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();
            Log($"\tRESPONSE: {responseString}");
            T result = JsonConvert.DeserializeObject<T>(responseString);
            return result;
        }

        private async Task<T> AuthedPost<T>(string url, JObject data)
        {
            if (!IsAuthed())
            {
                throw new Exception("Trying to make a request that requires authentication while not authenticated");
            }

            return await Post<T>(url, data, authRequired: true);
        }

        private HttpClient GetHttpClient(bool authRequired = false)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", AuthHeaders.UserAgent);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(AuthHeaders.Accept));
            client.DefaultRequestHeaders.Add("Accept-Language", AuthHeaders.AcceptLanguage);
            client.DefaultRequestHeaders.Add("X-Goog-AuthUser", AuthHeaders.GoogAuthUser);
            client.DefaultRequestHeaders.Add("x-origin", AuthHeaders.Origin);
            client.DefaultRequestHeaders.Add("Cookie", AuthHeaders.Cookie);

            if (authRequired)
            {
                string auth = GetAuthorization(AuthHeaders.Cookie, AuthHeaders.Origin);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("SAPISIDHASH", auth);
            }

            return client;
        }

        #endregion

        #region Utils

        private string GetAuthorization(string cookie, string origin)
        {
            string sapisid = GetSapsidFromCookie(cookie);
            string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            string authToEncode = $"{timestamp} {sapisid} {origin}";
            byte[] bytes = Encoding.UTF8.GetBytes(authToEncode);
            var sha = SHA1.Create();
            byte[] digest = sha.ComputeHash(bytes);
            string decoded = String.Concat(Array.ConvertAll(digest, x => x.ToString("X2").ToLower()));
            string auth = $"{timestamp}_{decoded}";
            return auth;
        }

        private string GetSapsidFromCookie(string cookie)
        {
            string pattern = "SAPISID=(?<sapisid>[^;]+);";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(cookie);
            string sapisid = match.Groups["sapisid"].Value;
            return sapisid;
        }

        private string GetYTMUrl(string endpoint)
        {
            return $"{BaseUrl}/{endpoint}{Params}";
        }

        private JObject PrepareBrowse(string endpoint, string id)
        {
            return JObject.FromObject(new
            {
                browseEndpointContextSupportedConfigs = new
                {
                    browseEndpointContextMusicConfig = new
                    {
                        pageType = $"MUSIC_PAGE_TYPE_{endpoint}"
                    }
                },
                browseId = id
            });
        }

        private void Log(string str)
        {
            if (Logger != null)
            {
                Logger.Log(str);
            }
        }

        #endregion

    }
}
