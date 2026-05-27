using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CctqMonitor;

public sealed class ConfigStore
{
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
    private readonly string _path;

    public ConfigStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CCTQ Monitor");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            return new AppConfig();
        }

        try
        {
            var stored = Serializer.Deserialize<StoredConfig>(File.ReadAllText(_path));
            if (stored is null)
            {
                return new AppConfig();
            }

            return new AppConfig
            {
                BaseUrl = string.IsNullOrWhiteSpace(stored.BaseUrl) ? "https://www.cctq.ai" : stored.BaseUrl,
                AccessToken = Dpapi.Unprotect(stored.AccessTokenProtected ?? ""),
                UserId = stored.UserId ?? "",
                WindowLeft = stored.WindowLeft,
                WindowTop = stored.WindowTop,
                IsLocked = stored.IsLocked,
                IsTopmost = stored.IsTopmost
            };
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var stored = new StoredConfig
        {
            BaseUrl = NormalizeBaseUrl(config.BaseUrl),
            AccessTokenProtected = Dpapi.Protect(config.AccessToken),
            UserId = config.UserId.Trim(),
            WindowLeft = config.WindowLeft,
            WindowTop = config.WindowTop,
            IsLocked = config.IsLocked,
            IsTopmost = config.IsTopmost
        };

        File.WriteAllText(_path, Serializer.Serialize(stored));
    }

    public static string NormalizeBaseUrl(string value)
    {
        var baseUrl = string.IsNullOrWhiteSpace(value) ? "https://www.cctq.ai" : value.Trim();
        return baseUrl.TrimEnd('/');
    }

    private sealed class StoredConfig
    {
        public string BaseUrl { get; set; }
        public string AccessTokenProtected { get; set; }
        public string UserId { get; set; }
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public bool IsLocked { get; set; }
        public bool IsTopmost { get; set; }
    }
}
