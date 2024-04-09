using Semver;
using Stardrop.Models.Data;
using Stardrop.Models.Data.Enums;
using Stardrop.Models.Nexus;
using Stardrop.Models.Nexus.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Stardrop.Utilities.External
{
    public static class NexusClientFactory
    {
        private static readonly Uri _baseUrl = new Uri("https://api.nexusmods.com/v1/");
        private static Nexus? _activeClient = null;

        public static string? GetCachedKey()
        {
            if (Program.settings.NexusDetails?.Key is null || File.Exists(Pathing.GetNotionCachePath()) is false)
            {
                return null;
            }

            var pairedKeys = JsonSerializer.Deserialize<PairedKeys>(File.ReadAllText(Pathing.GetNotionCachePath()), new JsonSerializerOptions { AllowTrailingCommas = true });
            if (pairedKeys?.Vector is null || pairedKeys?.Lock is null)
            {
                return null;
            }

            try
            {
                return SimpleObscure.Decrypt(Program.settings.NexusDetails.Key, pairedKeys.Lock, pairedKeys.Vector);
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to parse API key when requested: {ex}");
            }

            return null;
        }

        public static async Task<Nexus?> GetClient(string apiKey)
        {            
            // TODO: Force-refresh the client if we get a different API key? Gotta check against existing in that case...
            if (_activeClient != null)
            {
                return _activeClient;
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("apiKey", apiKey);
            client.DefaultRequestHeaders.Add("Application-Name", "Stardrop");
            client.DefaultRequestHeaders.Add("Application-Version", Program.ApplicationVersion);
            client.DefaultRequestHeaders.Add("User-Agent", $"Stardrop/{Program.ApplicationVersion} {Environment.OSVersion}");
            
            try
            {
                var response = await client.GetAsync(new Uri(_baseUrl, "users/validate"));
                if (!response.IsSuccessStatusCode || response.Content == null)
                {
                    Program.helper.Log($"Call to Nexus Mods failed. HTTP status code: {response.StatusCode}, {response.ReasonPhrase}");
                    if (response.Content == null)
                    {
                        Program.helper.Log($"No response from Nexus Mods!");
                    }
                    else
                    {
                        Program.helper.Log($"Response from Nexus Mods:\n{await response.Content.ReadAsStringAsync()}");
                    }
                    return null;
                }                

               
                string content = await response.Content.ReadAsStringAsync();
                Validate validationModel = JsonSerializer.Deserialize<Validate>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                if (validationModel is null || String.IsNullOrEmpty(validationModel.Message) is false)
                {
                    Program.helper.Log($"Unable to validate given API key for Nexus Mods");
                    Program.helper.Log($"Response from Nexus Mods:\n{content}");

                    return null;
                }
                
                if (Program.settings.NexusDetails is null)
                {
                    return null;
                }

                Program.settings.NexusDetails.Username = validationModel.Name;
                Program.settings.NexusDetails.IsPremium = validationModel.IsPremium;

                _activeClient = new Nexus(Program.settings.NexusDetails, client, response.Headers);
                return _activeClient;
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to validate user's API key for Nexus Mods: {ex}", Helper.Status.Alert);
                return null;
            }                       
        }
    }

    public class Nexus
    {
        private const string _nxmPattern = @"nxm:\/\/(?<domain>stardewvalley)\/mods\/(?<mod>[0-9]+)\/files\/(?<file>[0-9]+)\?key=(?<key>.*)&expires=(?<expiry>[0-9]+)&user_id=(?<user>[0-9]+)";

        private readonly HttpClient _client;
        private readonly NexusUser _settings;

        private int _dailyRequestsRemaining;
        private int _dailyRequestsLimit;

        // TODO: Replace with event handler, or messaging, or something.
        //private MainWindowViewModel _displayModel;

        public Nexus(NexusUser settings, HttpClient client, HttpResponseHeaders initialValidateResponseHeaders)
        {
            _settings = settings;
            _client = client;
            UpdateRequestCounts(initialValidateResponseHeaders);
        }       

        public async Task<ModDetails?> GetModDetailsViaNXM(NXM nxmData)
        {
            if (nxmData.Link is null)
            {
                return null;
            }

            var match = Regex.Match(Regex.Unescape(nxmData.Link), _nxmPattern);
            if (match.Success is false || match.Groups["domain"].ToString().ToLower() != "stardewvalley" || Int32.TryParse(match.Groups["mod"].ToString(), out int modId) is false)
            {
                return null;
            }

            try
            {
                var response = await _client.GetAsync(new Uri($"games/stardewvalley/mods/{modId}.json"));
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content is not null)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    ModDetails modDetails = JsonSerializer.Deserialize<ModDetails>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (modDetails is null)
                    {
                        Program.helper.Log($"Unable to get mod details for the mod {modId} on Nexus Mods");
                        Program.helper.Log($"Response from Nexus Mods:\n{content}");

                        return null;
                    }

                    UpdateRequestCounts(response.Headers);

                    return modDetails;
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Program.helper.Log($"Bad status given from Nexus Mods: {response.StatusCode}");
                        if (response.Content is not null)
                        {
                            Program.helper.Log($"Response from Nexus Mods:\n{await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    else if (response.Content is null)
                    {
                        Program.helper.Log($"No response from Nexus Mods!");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Unable to get mod details for the mod {modId} on Nexus Mods: {ex}", Helper.Status.Alert);
            }            

            return null;
        }

        public async Task<ModFile?> GetFileByVersion(string apiKey, int modId, string version, string? modFlag = null)
        {
            if (SemVersion.TryParse(version.Replace("v", String.Empty), SemVersionStyles.Any, out var targetVersion) is false)
            {
                Program.helper.Log($"Unable to parse given target version {version}");
                return null;
            }

            Program.helper.Log($"Requesting version {version} of mod {modId}{(String.IsNullOrEmpty(modFlag) is false ? $" with flag {modFlag}" : String.Empty)}");

            try
            {
                var response = await _client.GetAsync(new Uri($"games/stardewvalley/mods/{modId}/files.json"));
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content is not null)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    ModFiles modFiles = JsonSerializer.Deserialize<ModFiles>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (modFiles is null || modFiles.Files is null || modFiles.Files.Count == 0)
                    {
                        Program.helper.Log($"Unable to get the mod file for Nexus Mods");
                        Program.helper.Log($"Response from Nexus Mods:\n{content}");
                    }
                    else
                    {
                        ModFile? selectedFile = null;
                        foreach (var file in modFiles.Files.Where(x => String.IsNullOrEmpty(x.Version) is false && SemVersion.TryParse(x.Version.Replace("v", String.Empty), SemVersionStyles.Any, out var modVersion) && modVersion == targetVersion))
                        {
                            if (String.IsNullOrEmpty(modFlag) is false && ((String.IsNullOrEmpty(file.Name) is false && file.Name.Contains(modFlag, StringComparison.OrdinalIgnoreCase)) || (String.IsNullOrEmpty(file.Description) is false && file.Description.Contains(modFlag, StringComparison.OrdinalIgnoreCase))))
                            {
                                selectedFile = file;
                            }
                            else if (String.IsNullOrEmpty(modFlag) is true && String.IsNullOrEmpty(file.Category) is false && file.Category.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
                            {
                                selectedFile = file;
                            }
                        }

                        if (selectedFile is null)
                        {
                            Program.helper.Log($"Unable to get a matching file for the mod {modId} with version {version}{(String.IsNullOrEmpty(modFlag) is false ? $" and with the flag {modFlag}" : String.Empty)} via Nexus Mods: \n{String.Join("\n", modFiles.Files.Select(m => $"{m.Name} | {m.Version}"))}");
                        }

                        UpdateRequestCounts(response.Headers);

                        return selectedFile;
                    }
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Program.helper.Log($"Bad status given from Nexus Mods: {response.StatusCode}");
                        if (response.Content is not null)
                        {
                            Program.helper.Log($"Response from Nexus Mods:\n{await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    else if (response.Content is null)
                    {
                        Program.helper.Log($"No response from Nexus Mods!");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to get the mod file for Nexus Mods: {ex}", Helper.Status.Alert);
            }            

            return null;
        }

        public async Task<string?> GetFileDownloadLink(string apiKey, NXM nxmData, string? serverName = null)
        {
            if (nxmData.Link is null)
            {
                return null;
            }

            var match = Regex.Match(Regex.Unescape(nxmData.Link), _nxmPattern);
            if (match.Success is false || match.Groups["domain"].ToString().ToLower() != "stardewvalley" || Int32.TryParse(match.Groups["mod"].ToString(), out int modId) is false || Int32.TryParse(match.Groups["file"].ToString(), out int fileId) is false)
            {
                return null;
            }

            return await GetFileDownloadLink(apiKey, modId, fileId, match.Groups["key"].ToString(), match.Groups["expiry"].ToString(), serverName);
        }

        public async Task<string?> GetFileDownloadLink(string apiKey, int modId, int fileId, string? nxmKey = null, string? nxmExpiry = null, string? serverName = null)
        {
            if (String.IsNullOrEmpty(serverName) || Program.settings.NexusDetails.IsPremium is false)
            {
                serverName = "Nexus CDN";
            }

            try
            {
                string url = $"games/stardewvalley/mods/{modId}/files/{fileId}/download_link.json";
                if (String.IsNullOrEmpty(nxmKey) is false && String.IsNullOrEmpty(nxmExpiry) is false)
                {
                    url = $"{url}?key={nxmKey}&expires={nxmExpiry}";
                }
                var response = await _client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content is not null)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    List<DownloadLink> downloadLinks = JsonSerializer.Deserialize<List<DownloadLink>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (downloadLinks is null || downloadLinks.Count == 0)
                    {
                        Program.helper.Log($"Unable to get the download link for Nexus Mods");
                        Program.helper.Log($"Response from Nexus Mods:\n{content}");
                    }
                    else
                    {
                        UpdateRequestCounts(response.Headers);

                        var selectedFile = downloadLinks.FirstOrDefault(x => x.ShortName?.ToLower() == serverName.ToLower());
                        if (selectedFile is not null)
                        {
                            Program.helper.Log($"Requested download link from Nexus Mods using their {serverName} server");
                            return selectedFile.Uri;
                        }
                    }
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Program.helper.Log($"Bad status given from Nexus Mods: {response.StatusCode}");
                        if (response.Content is not null)
                        {
                            Program.helper.Log($"Response from Nexus Mods:\n{await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    else if (response.Content is null)
                    {
                        Program.helper.Log($"No response from Nexus Mods!");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to get the download link for Nexus Mods: {ex}", Helper.Status.Alert);
            }            

            return null;
        }

        public async Task<string?> DownloadFileAndGetPath(string uri, string fileName)
        {
            // Create a throwaway client
            // TODO: Investigate if we truly don't need ApiKey here. If so, remove it from DefaultRequestHeaders
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Application-Name", "Stardrop");
            client.DefaultRequestHeaders.Add("Application-Version", Program.ApplicationVersion);
            client.DefaultRequestHeaders.Add("User-Agent", $"Stardrop/{Program.ApplicationVersion} {Environment.OSVersion}");

            try
            {
                var stream = await client.GetStreamAsync(new Uri(uri));
                using (var fileStream = new FileStream(Path.Combine(Pathing.GetNexusPath(), fileName), FileMode.CreateNew))
                {
                    await stream.CopyToAsync(fileStream);
                }

                return Path.Combine(Pathing.GetNexusPath(), fileName);
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to download mod file for Nexus Mods: {ex}", Helper.Status.Alert);
            }
            client.Dispose();

            return null;
        }

        public async Task<List<Endorsement>> GetEndorsements(string apiKey)
        {
            try
            {
                var response = await _client.GetAsync(new Uri($"user/endorsements"));
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content is not null)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    List<Endorsement> endorsements = JsonSerializer.Deserialize<List<Endorsement>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (endorsements is null)
                    {
                        Program.helper.Log($"Unable to get endorsements for Nexus Mods");
                        Program.helper.Log($"Response from Nexus Mods:\n{content}");
                    }
                    else
                    {
                        endorsements = endorsements.Where(e => e.DomainName?.ToLower() == "stardewvalley").ToList();

                        UpdateRequestCounts(response.Headers);

                        return endorsements;
                    }
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Program.helper.Log($"Bad status given from Nexus Mods: {response.StatusCode}");
                        if (response.Content is not null)
                        {
                            Program.helper.Log($"Response from Nexus Mods:\n{await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    else if (response.Content is null)
                    {
                        Program.helper.Log($"No response from Nexus Mods!");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to get endorsements for Nexus Mods: {ex}", Helper.Status.Alert);
            }

            return new List<Endorsement>();
        }


        public async Task<EndorsementResponse> SetModEndorsement(string apiKey, int modId, bool isEndorsed)
        {
            try
            {
                var requestPackage = new StringContent("{\"Version\":\"1.0.0\"}", Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(new Uri($"games/stardewvalley/mods/{modId}/{(isEndorsed is true ? "endorse.json" : "abstain.json")}"), requestPackage);
                if (response.Content is not null)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    EndorsementResult endorsementResult = JsonSerializer.Deserialize<EndorsementResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (endorsementResult is null)
                    {
                        Program.helper.Log($"Unable to set endorsement for Nexus Mods");
                        Program.helper.Log($"Response from Nexus Mods:\n{content}");

                        return EndorsementResponse.Unknown;
                    }

                    UpdateRequestCounts(response.Headers);

                    switch (endorsementResult.Status?.ToUpper())
                    {
                        case "ENDORSED":
                            return EndorsementResponse.Endorsed;
                        case "ABSTAINED":
                            return EndorsementResponse.Abstained;
                        case "ERROR":
                            var parsedMessage = endorsementResult.Message?.ToUpper();
                            if (parsedMessage == "IS_OWN_MOD")
                            {
                                return EndorsementResponse.IsOwnMod;
                            }
                            else if (parsedMessage == "TOO_SOON_AFTER_DOWNLOAD")
                            {
                                return EndorsementResponse.TooSoonAfterDownload;
                            }
                            else if (parsedMessage == "NOT_DOWNLOADED_MOD")
                            {
                                return EndorsementResponse.NotDownloadedMod;
                            }
                            Program.helper.Log(parsedMessage);
                            break;
                        default:
                            Program.helper.Log($"Unhandled status for endorsement: {endorsementResult.Status} | {endorsementResult.Message}");
                            break;
                    }
                }
                else
                {
                    Program.helper.Log($"No response from Nexus Mods! Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Program.helper.Log($"Failed to set endorsement for Nexus Mods: {ex}", Helper.Status.Alert);
            }

            return EndorsementResponse.Unknown;
        }

        private void UpdateRequestCounts(HttpResponseHeaders headers)
        {
            if (headers.TryGetValues("x-rl-daily-limit", out var limitValues) && Int32.TryParse(limitValues.First(), out int dailyLimit))
            {
                _dailyRequestsLimit = dailyLimit;
            }

            if (headers.TryGetValues("x-rl-daily-remaining", out var remainingValues) && Int32.TryParse(remainingValues.First(), out int dailyRemaining))
            {
                _dailyRequestsRemaining = dailyRemaining;
            }

           // TODO: Fire event or something
            //_displayModel.NexusLimits = $"(Remaining Daily Requests: {_dailyRequestsRemaining}) ";
        }
    }
}
