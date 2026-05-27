using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CctqMonitor;

public sealed class CctqApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

    public CctqApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<MonitorSnapshot> FetchSnapshotAsync(AppConfig config, CancellationToken cancellationToken)
    {
        if (!config.HasCredentials)
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "需要配置");
        }

        var baseUrl = ConfigStore.NormalizeBaseUrl(config.BaseUrl);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var statusJson = await GetPublicJsonAsync($"{baseUrl}/api/status", cancellationToken);
            var quotaDisplay = QuotaDisplayConfig.FromStatus(statusJson);

            var userJson = await GetJsonAsync($"{baseUrl}/api/user/self", config, cancellationToken);
            var balanceQuota = ReadDecimal(userJson, "data", "quota");

            var now = DateTimeOffset.Now;
            var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
            var statUrl = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/api/log/self/stat?start_timestamp={1}&end_timestamp={2}",
                baseUrl,
                startOfDay.ToUnixTimeSeconds(),
                now.ToUnixTimeSeconds());

            var statJson = await GetJsonAsync(statUrl, config, cancellationToken);
            stopwatch.Stop();

            var todayUsageQuota = ReadDecimal(statJson, "data", "quota");
            var light = ToLight(stopwatch.Elapsed);

            return new MonitorSnapshot(
                quotaDisplay.Convert(balanceQuota),
                quotaDisplay.Convert(todayUsageQuota),
                light,
                now,
                "已更新",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "请求超时");
        }
        catch (HttpRequestException)
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "连接失败");
        }
        catch (UnauthorizedAccessException)
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "认证失败");
        }
        catch (InvalidOperationException)
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "数据异常");
        }
        catch
        {
            return new MonitorSnapshot(0, 0, ConnectionLight.Gray, DateTimeOffset.Now, "认证失败");
        }
    }

    private async Task<Dictionary<string, object>> GetJsonAsync(string url, AppConfig config, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.AccessToken.Trim()}");
        request.Headers.TryAddWithoutValidation("New-Api-User", config.UserId.Trim());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var document = _serializer.Deserialize<Dictionary<string, object>>(json);

        if (document is null)
        {
            throw new InvalidOperationException("Empty JSON response.");
        }

        if (document.TryGetValue("success", out var success) &&
            success is bool successValue &&
            !successValue)
        {
            throw new UnauthorizedAccessException("API returned success=false.");
        }

        return document;
    }

    private async Task<Dictionary<string, object>> GetPublicJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var document = _serializer.Deserialize<Dictionary<string, object>>(json);
        if (document is null)
        {
            throw new InvalidOperationException("Empty JSON response.");
        }

        return document;
    }

    private static decimal ReadDecimal(Dictionary<string, object> root, string parent, string name)
    {
        if (!root.TryGetValue(parent, out var dataObject) ||
            dataObject is not Dictionary<string, object> data ||
            !data.TryGetValue(name, out var value))
        {
            return 0;
        }

        switch (value)
        {
            case decimal decimalValue:
                return decimalValue;
            case int intValue:
                return intValue;
            case long longValue:
                return longValue;
            case double doubleValue:
                return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
            case string stringValue when decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                return parsed;
            default:
                return 0;
        }
    }

    private static ConnectionLight ToLight(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 3)
        {
            return ConnectionLight.Green;
        }

        if (elapsed.TotalSeconds <= 10)
        {
            return ConnectionLight.Yellow;
        }

        return ConnectionLight.Red;
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class QuotaDisplayConfig
    {
        private QuotaDisplayConfig(decimal quotaPerUnit, string displayType, decimal exchangeRate)
        {
            QuotaPerUnit = quotaPerUnit <= 0 ? 1 : quotaPerUnit;
            DisplayType = string.IsNullOrWhiteSpace(displayType) ? "USD" : displayType.ToUpperInvariant();
            ExchangeRate = exchangeRate <= 0 ? 1 : exchangeRate;
        }

        private decimal QuotaPerUnit { get; }
        private string DisplayType { get; }
        private decimal ExchangeRate { get; }

        public static QuotaDisplayConfig FromStatus(Dictionary<string, object> statusRoot)
        {
            if (!statusRoot.TryGetValue("data", out var dataObject) ||
                dataObject is not Dictionary<string, object> data)
            {
                return new QuotaDisplayConfig(1, "USD", 1);
            }

            var quotaPerUnit = ReadDecimalValue(data, "quota_per_unit", 1);
            var displayType = ReadStringValue(data, "quota_display_type", "USD");
            var exchangeRate = displayType.ToUpperInvariant() switch
            {
                "CNY" => ReadDecimalValue(data, "usd_exchange_rate", 1),
                "CUSTOM" => ReadDecimalValue(data, "custom_currency_exchange_rate", 1),
                _ => 1
            };

            return new QuotaDisplayConfig(quotaPerUnit, displayType, exchangeRate);
        }

        public decimal Convert(decimal quota)
        {
            if (DisplayType == "TOKENS")
            {
                return quota;
            }

            return quota / QuotaPerUnit * ExchangeRate;
        }

        private static string ReadStringValue(Dictionary<string, object> data, string key, string fallback)
        {
            return data.TryGetValue(key, out var value) && value != null
                ? value.ToString()
                : fallback;
        }

        private static decimal ReadDecimalValue(Dictionary<string, object> data, string key, decimal fallback)
        {
            if (!data.TryGetValue(key, out var value))
            {
                return fallback;
            }

            switch (value)
            {
                case decimal decimalValue:
                    return decimalValue;
                case int intValue:
                    return intValue;
                case long longValue:
                    return longValue;
                case double doubleValue:
                    return System.Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
                case string stringValue when decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return fallback;
            }
        }
    }
}
