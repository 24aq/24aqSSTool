using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using TwentyFourAqSSTool.Models;

namespace TwentyFourAqSSTool.Services;

public sealed class ToolService
{
    private readonly HttpClient _http = new();
    public string InstallRoot { get; }

    public ToolService()
    {
        InstallRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "24aqSSTool",
            "Tools");

        Directory.CreateDirectory(InstallRoot);

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("24aqSSTool", "1.0"));
    }

    public async Task<List<ToolEntry>> LoadToolsAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tools.json");

        if (!File.Exists(path))
            return new List<ToolEntry>();

        var json = await File.ReadAllTextAsync(path);

        return JsonSerializer.Deserialize<List<ToolEntry>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<ToolEntry>();
    }

    public string GetToolFolder(ToolEntry tool)
    {
        var category = Sanitize(tool.Category);
        var name = Sanitize(tool.Name);
        var folder = Path.Combine(InstallRoot, category, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string GetToolPath(ToolEntry tool)
    {
        var folder = GetToolFolder(tool);

        var launchable = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(IsLaunchable)
            .OrderBy(LaunchPriority)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(launchable))
            return launchable;

        if (!string.IsNullOrWhiteSpace(tool.FileName))
            return Path.Combine(folder, tool.FileName);

        return Path.Combine(folder, Sanitize(tool.Name) + ".exe");
    }

    public bool HasDownload(ToolEntry tool)
    {
        return Uri.TryCreate(tool.DownloadUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    public bool HasHomePage(ToolEntry tool)
    {
        return Uri.TryCreate(tool.HomePage, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    public async Task<string> DownloadAsync(
        ToolEntry tool,
        IProgress<double>? progress = null)
    {
        if (!HasDownload(tool))
            throw new InvalidOperationException(
                $"No valid GitHub download URL is configured for {tool.Name}.");

        var resolved = await ResolveDownloadAsync(tool);
        var folder = GetToolFolder(tool);
        var target = Path.Combine(folder, resolved.FileName);
        var temp = target + ".download";

        using var response = await _http.GetAsync(
            resolved.Url,
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(temp);

        var buffer = new byte[81920];
        long readTotal = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer);
            if (read <= 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read));
            readTotal += read;

            if (total is > 0)
                progress?.Report((double)readTotal / total.Value);
        }

        await output.FlushAsync();
        output.Close();

        if (!string.IsNullOrWhiteSpace(tool.Sha256))
        {
            var actual = ComputeSha256(temp);
            var expected = tool.Sha256.Replace(" ", "").Replace("-", "");

            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temp);
                throw new InvalidOperationException(
                    $"SHA-256 mismatch.\nExpected: {expected}\nActual: {actual}");
            }
        }

        File.Move(temp, target, true);

        if (Path.GetExtension(target).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(target, folder, true);

            var extracted = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(IsLaunchable)
                .OrderBy(LaunchPriority)
                .FirstOrDefault();

            return extracted ?? target;
        }

        return target;
    }

    public void Launch(ToolEntry tool)
    {
        var path = GetToolPath(tool);

        if (!File.Exists(path))
            throw new FileNotFoundException(
                "Tool has not been downloaded yet.",
                path);

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".ps1")
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{path}\"",
                WorkingDirectory = Path.GetDirectoryName(path)!,
                UseShellExecute = true
            });
            return;
        }

        if (ext is ".cmd" or ".bat")
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{path}\"",
                WorkingDirectory = Path.GetDirectoryName(path)!,
                UseShellExecute = true
            });
            return;
        }

        if (ext == ".jar")
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "javaw.exe",
                Arguments = $"-jar \"{path}\"",
                WorkingDirectory = Path.GetDirectoryName(path)!,
                UseShellExecute = true
            });
            return;
        }

        if (ext == ".zip")
        {
            OpenToolFolder(tool);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = true
        });
    }

    public void OpenHomePage(ToolEntry tool)
    {
        if (!HasHomePage(tool))
            throw new InvalidOperationException(
                $"No GitHub page is configured for {tool.Name}.");

        Process.Start(new ProcessStartInfo(tool.HomePage!)
        {
            UseShellExecute = true
        });
    }

    public void OpenToolFolder(ToolEntry tool)
    {
        var folder = GetToolFolder(tool);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true
        });
    }

    public void OpenInstallFolder()
    {
        Directory.CreateDirectory(InstallRoot);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{InstallRoot}\"",
            UseShellExecute = true
        });
    }

    public void ClearDownloadedFiles()
    {
        if (Directory.Exists(InstallRoot))
            Directory.Delete(InstallRoot, true);

        Directory.CreateDirectory(InstallRoot);
    }

    public void OpenCmd()
    {
        Directory.CreateDirectory(InstallRoot);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = InstallRoot,
            UseShellExecute = true
        });
    }

    private async Task<(string Url, string FileName)> ResolveDownloadAsync(ToolEntry tool)
    {
        var uri = new Uri(tool.DownloadUrl);

        if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = string.IsNullOrWhiteSpace(tool.FileName)
                ? Path.GetFileName(uri.LocalPath)
                : tool.FileName;

            return (tool.DownloadUrl, fileName);
        }

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.AbsolutePath.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase))
            {
                var directFile = string.IsNullOrWhiteSpace(tool.FileName)
                    ? Path.GetFileName(uri.LocalPath)
                    : tool.FileName;

                return (tool.DownloadUrl, directFile);
            }

            var parts = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                throw new InvalidOperationException("Invalid GitHub repository URL.");

            var owner = parts[0];
            var repo = parts[1];
            var api = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using var response = await _http.GetAsync(api);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("assets", out var assets))
                throw new InvalidOperationException(
                    $"No release assets found for {tool.Name}.");

            var candidates = new List<(string Name, string Url, int Score)>();

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                    continue;

                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (ext is not (".exe" or ".zip" or ".cmd" or ".bat" or ".ps1" or ".jar"))
                    continue;

                var lower = name.ToLowerInvariant();
                var score = ext switch
                {
                    ".exe" => 100,
                    ".zip" => 80,
                    ".cmd" => 60,
                    ".bat" => 60,
                    ".ps1" => 50,
                    ".jar" => 45,
                    _ => 0
                };

                if (lower.Contains("windows") || lower.Contains("win"))
                    score += 40;

                if (lower.Contains("x64") || lower.Contains("amd64"))
                    score += 25;

                if (lower.Contains("portable"))
                    score += 10;

                if (lower.Contains("linux") || lower.Contains("mac") ||
                    lower.Contains("darwin") || lower.Contains("arm64"))
                    score -= 100;

                candidates.Add((name, url, score));
            }

            var best = candidates
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(best.Url))
                throw new InvalidOperationException(
                    $"No Windows .exe/.zip/.cmd/.bat/.ps1/.jar release asset found for {tool.Name}.");

            return (best.Url, best.Name);
        }

        var directName = string.IsNullOrWhiteSpace(tool.FileName)
            ? Path.GetFileName(uri.LocalPath)
            : tool.FileName;

        return (tool.DownloadUrl, directName);
    }

    private static bool IsLaunchable(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".exe" or ".cmd" or ".bat" or ".ps1" or ".jar";
    }

    private static int LaunchPriority(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".exe" => 0,
            ".cmd" => 1,
            ".bat" => 2,
            ".ps1" => 3,
            ".jar" => 4,
            _ => 9
        };
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string ComputeSha256(string file)
    {
        using var stream = File.OpenRead(file);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}