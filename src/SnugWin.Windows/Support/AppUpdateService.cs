using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnugWin.Core.Support;

namespace SnugWin.Windows.Support;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string ReleaseNotes,
    ReleasePackageKind PackageKind,
    string PackageAssetName,
    string? PackageDownloadUrl,
    string? ChecksumsDownloadUrl);

public sealed record PreparedUpdate(
    string Version,
    string StagingRoot,
    string PayloadDir,
    string TargetAppDir);

public sealed class AppUpdateService
{
    private const string ReleaseRepository = "Ruyi231/Snug";
    private const string ReleaseWebBase = "https://github.com/" + ReleaseRepository + "/releases";
    private const string LatestApiUrl = "https://api.github.com/repos/" + ReleaseRepository + "/releases/latest";
    private const string LatestWebUrl = ReleaseWebBase + "/latest";

    private static readonly HttpClient ApiHttp = CreateHttpClient(TimeSpan.FromSeconds(12));
    private static readonly HttpClient DownloadHttp = CreateHttpClient(TimeSpan.FromMinutes(10));

    public static string CurrentVersion
    {
        get
        {
            var infoVer = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
                return AppVersionHelper.Normalize(infoVer);

            var asmVer = Assembly.GetEntryAssembly()?.GetName().Version;
            return asmVer is null ? "1.0.0" : $"{asmVer.Major}.{asmVer.Minor}.{Math.Max(0, asmVer.Build)}";
        }
    }

    public static ReleasePackageKind CurrentPackageKind =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"))
            ? ReleasePackageKind.SelfContained
            : ReleasePackageKind.FrameworkDependent;

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        string current = CurrentVersion;
        ReleasePackageKind kind = CurrentPackageKind;

        UpdateReleaseInfo? release = null;
        try
        {
            using var resp = await ApiHttp.GetAsync(LatestApiUrl, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                release = GitHubReleaseParser.ParseLatestRelease(json);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"请求 GitHub Release API 失败，尝试跳转兜底：{ex.Message}");
        }

        if (release is null)
            release = await FallbackCheckViaRedirectAsync(ct).ConfigureAwait(false);

        if (release is null)
            throw new InvalidOperationException("无法获取最新版本信息，请检查网络连接或稍后再试。");

        string preferredName = AppVersionHelper.PreferredAssetName(release.TagName, kind);
        var pkgAsset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
        var checksumAsset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase));

        bool hasUpdate = AppVersionHelper.IsNewer(release.TagName, current);

        return new UpdateCheckResult(
            HasUpdate: hasUpdate,
            CurrentVersion: current,
            LatestVersion: release.NormalizedVersion,
            ReleaseUrl: string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? $"{ReleaseWebBase}/tag/{release.TagName}"
                : release.HtmlUrl,
            ReleaseNotes: release.Body,
            PackageKind: kind,
            PackageAssetName: preferredName,
            PackageDownloadUrl: pkgAsset?.DownloadUrl,
            ChecksumsDownloadUrl: checksumAsset?.DownloadUrl);
    }

    public async Task<PreparedUpdate> DownloadAndPrepareAsync(
        UpdateCheckResult check,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(check.PackageDownloadUrl))
            throw new InvalidOperationException($"当前版本资产 {check.PackageAssetName} 尚未提供直接下载地址。");

        string stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "SnugWin-Update",
            $"{check.LatestVersion}-{Guid.NewGuid():N}"[..Math.Min(32, check.LatestVersion.Length + 9)]);
        Directory.CreateDirectory(stagingRoot);

        string zipPath = Path.Combine(stagingRoot, check.PackageAssetName);
        string payloadDir = Path.Combine(stagingRoot, "payload");

        string? checksumText = null;
        if (!string.IsNullOrWhiteSpace(check.ChecksumsDownloadUrl))
        {
            try
            {
                checksumText = await ApiHttp.GetStringAsync(check.ChecksumsDownloadUrl, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warn($"下载 checksums.txt 失败：{ex.Message}");
            }
        }

        await DownloadFileWithProgressAsync(check.PackageDownloadUrl, zipPath, progress, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(checksumText)
            && ChecksumParser.TryFindSha256(checksumText, check.PackageAssetName, out string expectedSha))
        {
            string actualSha = await ComputeSha256Async(zipPath, ct).ConfigureAwait(false);
            if (!actualSha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(stagingRoot, recursive: true); } catch { }
                throw new InvalidDataException($"安装包 SHA-256 校验失败（期望 {expectedSha}，实际 {actualSha}），已中止更新。");
            }
            Log.Info($"更新包 SHA-256 校验通过：{actualSha}");
        }

        ZipFile.ExtractToDirectory(zipPath, payloadDir, overwriteFiles: true);
        string targetExe = Path.Combine(payloadDir, "SnugWin.App.exe");
        if (!File.Exists(targetExe))
        {
            try { Directory.Delete(stagingRoot, recursive: true); } catch { }
            throw new InvalidDataException("更新包内未找到 SnugWin.App.exe。");
        }

        return new PreparedUpdate(
            Version: check.LatestVersion,
            StagingRoot: stagingRoot,
            PayloadDir: payloadDir,
            TargetAppDir: AppContext.BaseDirectory.TrimEnd('\\', '/'));
    }

    public static void LaunchApplyScript(PreparedUpdate update, bool relaunchMinimized = false)
    {
        int pid = Environment.ProcessId;
        string scriptPath = Path.Combine(update.StagingRoot, "apply-update.ps1");
        string exePath = Path.Combine(update.TargetAppDir, "SnugWin.App.exe");
        string argLine = relaunchMinimized ? $"'{StartupTaskDefinition.MinimizedArg}'" : "@()";

        string script = $$"""
            $ErrorActionPreference = 'Stop'
            $targetPid = {{pid}}
            $deadline = (Get-Date).AddSeconds(30)
            while ((Get-Date) -lt $deadline) {
                $p = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
                if (-not $p) { break }
                Start-Sleep -Milliseconds 250
            }
            Start-Sleep -Milliseconds 350

            $src = '{{EscapePsSingle(update.PayloadDir)}}'
            $dst = '{{EscapePsSingle(update.TargetAppDir)}}'
            Get-ChildItem -Path $src -Recurse -File | ForEach-Object {
                $rel = $_.FullName.Substring($src.Length).TrimStart('\', '/')
                $outFile = Join-Path $dst $rel
                $outDir = Split-Path -Parent $outFile
                if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
                Copy-Item -Path $_.FullName -Destination $outFile -Force
            }

            $launchExe = '{{EscapePsSingle(exePath)}}'
            $launchArgs = {{argLine}}
            if ($launchArgs.Count -gt 0) {
                Start-Process -FilePath $launchExe -ArgumentList $launchArgs -WorkingDirectory $dst
            } else {
                Start-Process -FilePath $launchExe -WorkingDirectory $dst
            }

            Start-Sleep -Seconds 2
            Remove-Item -Path '{{EscapePsSingle(update.StagingRoot)}}' -Recurse -Force -ErrorAction SilentlyContinue
            """;

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }

    private static async Task<UpdateReleaseInfo?> FallbackCheckViaRedirectAsync(CancellationToken ct)
    {
        using var resp = await ApiHttp.GetAsync(LatestWebUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        string? finalUrl = resp.RequestMessage?.RequestUri?.AbsoluteUri;
        if (string.IsNullOrWhiteSpace(finalUrl)) return null;

        const string marker = "/releases/tag/";
        int idx = finalUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        string tag = finalUrl[(idx + marker.Length)..].Trim('/');
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string ver = AppVersionHelper.Normalize(tag);
        string baseDl = $"{ReleaseWebBase}/download/{tag}";
        string selfZip = AppVersionHelper.PreferredAssetName(tag, ReleasePackageKind.SelfContained);
        string fddZip = AppVersionHelper.PreferredAssetName(tag, ReleasePackageKind.FrameworkDependent);

        return new UpdateReleaseInfo(
            tag,
            ver,
            finalUrl,
            string.Empty,
            [
                new UpdateAssetInfo(selfZip, $"{baseDl}/{selfZip}", 0),
                new UpdateAssetInfo(fddZip, $"{baseDl}/{fddZip}", 0),
                new UpdateAssetInfo("checksums.txt", $"{baseDl}/checksums.txt", 0)
            ]);
    }

    private static async Task DownloadFileWithProgressAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        using var resp = await DownloadHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(destinationPath);

        byte[] buffer = new byte[65536];
        long readTotal = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            readTotal += read;
            if (total is > 0)
                progress?.Report(Math.Clamp((double)readTotal / total.Value, 0.0, 1.0));
        }
        progress?.Report(1.0);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string EscapePsSingle(string value) => value.Replace("'", "''");

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SnugWin-Updater/1.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
