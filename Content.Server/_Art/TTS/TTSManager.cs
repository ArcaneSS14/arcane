using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Content.Shared._Art.CVars;
using Prometheus;
using Robust.Shared.Configuration;

namespace Content.Server._Art.TTS;

// ReSharper disable once InconsistentNaming
/// <summary>
/// TTS Manager for ntts.fdev.team API
/// </summary>
public sealed class TTSManager
{
    private static readonly Histogram RequestTimings = Metrics.CreateHistogram(
        "tts_req_timings",
        "Timings of TTS API requests",
        new HistogramConfiguration()
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(.1, 1.5, 10),
        });

    private static readonly Counter WantedCount = Metrics.CreateCounter(
        "tts_wanted_count",
        "Amount of wanted TTS audio.");

    private static readonly Counter ReusedCount = Metrics.CreateCounter(
        "tts_reused_count",
        "Amount of reused TTS audio from cache.");

    [Robust.Shared.IoC.Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;

    private readonly Dictionary<string, byte[]> _cache = new();
    private readonly HashSet<string> _cacheKeysSeq = new();
    private int _maxCachedCount = 200;

    public IReadOnlyDictionary<string, byte[]> Cache => _cache;
    public IReadOnlyCollection<string> CacheKeysSeq => _cacheKeysSeq;
    public int MaxCachedCount
    {
        get => _maxCachedCount;
        set
        {
            _maxCachedCount = value;
            ResetCache();
        }
    }

    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");

        _cfg.OnValueChanged(ArtCVars.TTSMaxCache, val =>
        {
            _maxCachedCount = val;
            ResetCache();
        }, true);
        _cfg.OnValueChanged(ArtCVars.TTSApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(ArtCVars.TTSApiToken, v =>
        {
            _apiToken = v;
            // Update Authorization header when token changes
            _httpClient.DefaultRequestHeaders.Authorization =
                string.IsNullOrEmpty(v) ? null : new AuthenticationHeaderValue("Bearer", v);
        }, true);
    }

    /// <summary>
    /// Generates audio with passed text using ntts.fdev.team API
    /// </summary>
    /// <param name="speaker">Identifier of speaker (e.g., "father_grigori")</param>
    /// <param name="text">Text to synthesize</param>
    /// <param name="effect">Optional audio effect (e.g., "echo", "reverb")</param>
    /// <returns>OGG audio bytes or null if failed</returns>
    public async Task<byte[]?> ConvertTextToSpeech(string speaker, string text, string? effect = null)
    {
        WantedCount.Inc();
        var cacheKey = GenerateCacheKey(speaker, text, effect);
        if (_cache.TryGetValue(cacheKey, out var data))
        {
            ReusedCount.Inc();
            _sawmill.Verbose($"Use cached sound for '{text}' speech by '{speaker}' speaker");
            return data;
        }

        if (string.IsNullOrWhiteSpace(_apiUrl))
        {
            _sawmill.Warning("TTS API URL is not configured");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            _sawmill.Warning("TTS API Token is not configured");
            return null;
        }

        _sawmill.Verbose($"Generate new audio for '{text}' speech by '{speaker}' speaker");

        var reqTime = DateTime.UtcNow;
        try
        {
            // Build request URL with query parameters
            var requestUrl = BuildRequestUrl(speaker, text, effect);

            var timeout = _cfg.GetCVar(ArtCVars.TTSApiTimeout);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            // GET request to ntts.fdev.team API
            var response = await _httpClient.GetAsync(requestUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _sawmill.Warning("TTS request was rate limited");
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _sawmill.Error("TTS request unauthorized - check API token");
                    return null;
                }

                _sawmill.Error($"TTS request returned bad status code: {response.StatusCode}");
                return null;
            }

            // API returns raw audio bytes directly
            var soundData = await response.Content.ReadAsByteArrayAsync(cts.Token);

            if (soundData.Length == 0)
            {
                _sawmill.Warning($"TTS API returned empty audio for '{text}'");
                return null;
            }

            // Add to cache
            _cache.TryAdd(cacheKey, soundData);
            _cacheKeysSeq.Add(cacheKey);

            // Evict old cache entries
            while (_cache.Count > _maxCachedCount && _cacheKeysSeq.Count > 0)
            {
                var oldestKey = _cacheKeysSeq.First();
                _cache.Remove(oldestKey);
                _cacheKeysSeq.Remove(oldestKey);
            }

            _sawmill.Debug($"Generated new audio for '{text}' speech by '{speaker}' speaker ({soundData.Length} bytes)");
            RequestTimings.WithLabels("Success").Observe((DateTime.UtcNow - reqTime).TotalSeconds);

            return soundData;
        }
        catch (TaskCanceledException)
        {
            RequestTimings.WithLabels("Timeout").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Timeout of request generation new audio for '{text}' speech by '{speaker}' speaker");
            return null;
        }
        catch (Exception e)
        {
            RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Failed of request generation new sound for '{text}' speech by '{speaker}' speaker\n{e}");
            return null;
        }
    }

    /// <summary>
    /// Build request URL for ntts.fdev.team API
    /// Format: GET /api/v1/tts?speaker=X&text=Y&ext=ogg
    /// </summary>
    private string BuildRequestUrl(string speaker, string text, string? effect)
    {
        var uriBuilder = new UriBuilder(_apiUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        query["speaker"] = speaker;
        query["text"] = text;
        query["ext"] = "ogg";

        if (!string.IsNullOrEmpty(effect))
            query["effect"] = effect;

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    public void ResetCache()
    {
        _cache.Clear();
        _cacheKeysSeq.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private string GenerateCacheKey(string speaker, string text, string? effect)
    {
        var keyData = Encoding.UTF8.GetBytes($"{speaker}/{text}/{effect ?? ""}");
        var hashBytes = System.Security.Cryptography.SHA256.HashData(keyData);
        return Convert.ToHexString(hashBytes);
    }
}
