namespace SignManager.Core.Models;

public sealed record BundleIdentity(
    string SourceBundleId,
    string EffectiveBundleId);
