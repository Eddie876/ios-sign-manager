namespace SignManager.Signing.Zsign;

public static class ZsignArgumentBuilder
{
    public static IReadOnlyList<string> Build(ZsignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>
        {
            "-k", request.PrivateKeyPath,
            "-c", request.CertificatePath,
            "-m", request.MobileProvisionPath,
            "-b", request.EffectiveBundleId,
            "-o", request.OutputIpaPath,
        };

        if (request.RemoveExtensions)
        {
            arguments.Add("-E");
        }

        arguments.Add(request.SourceIpaPath);
        return arguments;
    }
}
