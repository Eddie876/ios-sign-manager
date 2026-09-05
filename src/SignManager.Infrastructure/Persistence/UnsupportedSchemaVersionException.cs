namespace SignManager.Infrastructure.Persistence;

public sealed class UnsupportedSchemaVersionException(string documentName, int actualVersion, int expectedVersion)
    : InvalidOperationException($"Unsupported schema version for {documentName}. Expected {expectedVersion}, got {actualVersion}.")
{
    public string DocumentName { get; } = documentName;

    public int ActualVersion { get; } = actualVersion;

    public int ExpectedVersion { get; } = expectedVersion;
}
