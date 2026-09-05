using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SignManager.Core.Constants;

namespace SignManager.Signing.Ipa;

public sealed class IpaPreflightService
{
    public async Task<IpaPreflightResult> ValidateAndExtractAsync(IpaPreflightRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileInfo = new FileInfo(request.IpaPath);
        if (!fileInfo.Exists)
        {
            throw new IpaPreflightException(StableErrorCodes.InvalidIpa, "IPA file does not exist.");
        }

        if (fileInfo.Length > request.Limits.MaxUploadBytes)
        {
            throw new IpaPreflightException(StableErrorCodes.ZipLimitExceeded, "IPA exceeds max upload bytes.");
        }

        AppMetadata metadata;

        using (var archive = ZipFile.OpenRead(request.IpaPath))
        {
            ValidateArchiveEntries(archive, request.Limits);
            metadata = ExtractMetadata(archive);
        }

        var sha256 = await ComputeSha256Async(request.IpaPath, cancellationToken);
        return new IpaPreflightResult(metadata, fileInfo.Length, sha256);
    }

    private static void ValidateArchiveEntries(ZipArchive archive, IpaPreflightLimits limits)
    {
        if (archive.Entries.Count > limits.MaxEntries)
        {
            throw new IpaPreflightException(StableErrorCodes.ZipLimitExceeded, "IPA entries exceed limit.");
        }

        long totalExpanded = 0;

        foreach (var entry in archive.Entries)
        {
            ValidateEntryPath(entry.FullName);
            ValidateNotSymlink(entry);

            if (entry.Length > limits.MaxSingleEntryBytes)
            {
                throw new IpaPreflightException(StableErrorCodes.ZipLimitExceeded, "Single entry exceeds size limit.");
            }

            totalExpanded += entry.Length;
            if (totalExpanded > limits.MaxTotalExpandedBytes)
            {
                throw new IpaPreflightException(StableErrorCodes.ZipLimitExceeded, "Expanded content exceeds size limit.");
            }

            if (entry.CompressedLength > 0)
            {
                var ratio = (double)entry.Length / entry.CompressedLength;
                if (ratio > limits.MaxCompressionRatio)
                {
                    throw new IpaPreflightException(StableErrorCodes.ZipLimitExceeded, "Entry compression ratio exceeds limit.");
                }
            }
        }
    }

    private static void ValidateEntryPath(string path)
    {
        if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
        {
            throw new IpaPreflightException(StableErrorCodes.ZipPathTraversal, "Absolute ZIP entry path is not allowed.");
        }

        if (path.Contains("..", StringComparison.Ordinal))
        {
            throw new IpaPreflightException(StableErrorCodes.ZipPathTraversal, "Path traversal entry is not allowed.");
        }

        if (path.Contains(":", StringComparison.Ordinal))
        {
            throw new IpaPreflightException(StableErrorCodes.ZipPathTraversal, "Drive-qualified ZIP entry path is not allowed.");
        }
    }

    private static void ValidateNotSymlink(ZipArchiveEntry entry)
    {
        var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        const int symlinkMode = 0xA000;

        if (unixMode == symlinkMode)
        {
            throw new IpaPreflightException(StableErrorCodes.InvalidIpa, "Symlink entries are not supported.");
        }
    }

    private static AppMetadata ExtractMetadata(ZipArchive archive)
    {
        var appRoot = ResolveMainAppRoot(archive);

        var infoPlistEntry = archive.GetEntry($"{appRoot}Info.plist")
            ?? throw new IpaPreflightException(StableErrorCodes.InvalidIpa, "Main app Info.plist not found.");

        var infoPlist = LoadPlist(infoPlistEntry);

        var name = ReadOptionalStringKey(infoPlist, "CFBundleDisplayName")
            ?? ReadOptionalStringKey(infoPlist, "CFBundleName")
            ?? "Unknown";

        var bundleId = ReadRequiredStringKey(infoPlist, "CFBundleIdentifier");
        var version = ReadOptionalStringKey(infoPlist, "CFBundleShortVersionString") ?? "0";
        var build = ReadOptionalStringKey(infoPlist, "CFBundleVersion") ?? "0";
        var minimumOsVersion = ReadOptionalStringKey(infoPlist, "MinimumOSVersion");

        var extensions = ExtractExtensions(archive, appRoot);
        var entitlements = ExtractEntitlements(archive, appRoot);
        var hasWatchApp = archive.Entries.Any(e => e.FullName.StartsWith($"{appRoot}Watch/", StringComparison.Ordinal));
        var hasAppClips = archive.Entries.Any(e => e.FullName.StartsWith($"{appRoot}AppClips/", StringComparison.Ordinal));

        var warnings = new List<string>();
        if (extensions.Count > 0)
        {
            warnings.Add("Extensions detected in IPA.");
        }

        if (hasWatchApp)
        {
            warnings.Add("Watch app payload detected.");
        }

        if (hasAppClips)
        {
            warnings.Add("App Clips payload detected.");
        }

        return new AppMetadata(
            Name: name,
            SourceBundleId: bundleId,
            Version: version,
            Build: build,
            MinimumOsVersion: minimumOsVersion,
            Extensions: extensions,
            Entitlements: entitlements,
            HasWatchApp: hasWatchApp,
            HasAppClips: hasAppClips,
            Warnings: warnings);
    }

    private static string ResolveMainAppRoot(ZipArchive archive)
    {
        var appRoots = archive.Entries
            .Select(e => e.FullName)
            .Where(x => x.StartsWith("Payload/", StringComparison.Ordinal) && x.Contains(".app/", StringComparison.Ordinal))
            .Select(x =>
            {
                var payloadRelative = x["Payload/".Length..];
                var appSuffixIndex = payloadRelative.IndexOf(".app/", StringComparison.Ordinal);
                var appSegment = payloadRelative[..(appSuffixIndex + ".app".Length)];
                return $"Payload/{appSegment}/";
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (appRoots.Length != 1)
        {
            throw new IpaPreflightException(StableErrorCodes.InvalidIpa, "IPA must contain exactly one main app under Payload/*.app.");
        }

        return appRoots[0];
    }

    private static IReadOnlyList<string> ExtractExtensions(ZipArchive archive, string appRoot)
    {
        var prefix = $"{appRoot}PlugIns/";
        return archive.Entries
            .Select(e => e.FullName)
            .Where(x => x.StartsWith(prefix, StringComparison.Ordinal) && x.Contains(".appex/", StringComparison.Ordinal))
            .Select(x =>
            {
                var segment = x[prefix.Length..];
                var idx = segment.IndexOf(".appex", StringComparison.Ordinal);
                return segment[..(idx + ".appex".Length)];
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractEntitlements(ZipArchive archive, string appRoot)
    {
        var entitlementEntries = archive.Entries
            .Where(e => e.FullName.StartsWith(appRoot, StringComparison.Ordinal)
                && (e.FullName.EndsWith(".xcent", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.Contains("entitlements", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entitlementEntries)
        {
            try
            {
                var plist = LoadPlist(entry);
                foreach (var key in plist.Descendants("key"))
                {
                    keys.Add(key.Value);
                }
            }
            catch
            {
                // Skip malformed entitlement payloads in preflight metadata extraction.
            }
        }

        return keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static XDocument LoadPlist(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();

        var xmlStart = content.IndexOf("<?xml", StringComparison.Ordinal);
        var xmlEnd = content.LastIndexOf("</plist>", StringComparison.Ordinal);
        if (xmlStart >= 0 && xmlEnd > xmlStart)
        {
            content = content[xmlStart..(xmlEnd + "</plist>".Length)];
        }

        return XDocument.Parse(content);
    }

    private static string ReadRequiredStringKey(XDocument plist, string key)
        => ReadOptionalStringKey(plist, key)
        ?? throw new IpaPreflightException(StableErrorCodes.InvalidIpa, $"Info.plist is missing required key '{key}'.");

    private static string? ReadOptionalStringKey(XDocument plist, string key)
    {
        var keyNode = plist.Descendants("key").FirstOrDefault(x => x.Value == key);
        if (keyNode is null)
        {
            return null;
        }

        var valueNode = keyNode.ElementsAfterSelf().OfType<XElement>().FirstOrDefault();
        return valueNode?.Name.LocalName == "string" ? valueNode.Value : null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
