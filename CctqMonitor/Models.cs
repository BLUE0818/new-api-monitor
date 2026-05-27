using System;

namespace CctqMonitor;

public sealed class AppConfig
{
    public string BaseUrl { get; set; } = "https://www.cctq.ai";
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool IsLocked { get; set; }
    public bool IsTopmost { get; set; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(UserId);
}

public enum ConnectionLight
{
    Gray,
    Green,
    Yellow,
    Red
}

public sealed class MonitorSnapshot
{
    public MonitorSnapshot(decimal balance, decimal todayUsage, ConnectionLight light, DateTimeOffset refreshedAt, string statusText, TimeSpan? latency = null)
    {
        Balance = balance;
        TodayUsage = todayUsage;
        Light = light;
        RefreshedAt = refreshedAt;
        StatusText = statusText;
        Latency = latency;
    }

    public decimal Balance { get; }
    public decimal TodayUsage { get; }
    public ConnectionLight Light { get; }
    public DateTimeOffset RefreshedAt { get; }
    public string StatusText { get; }
    public TimeSpan? Latency { get; }
}
