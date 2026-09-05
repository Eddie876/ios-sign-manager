using SignManager.Core.Constants;
using SignManager.Core.Models;

namespace SignManager.Worker.Signing;

public sealed class UnavailableProvisioningMaterialProvider : IProvisioningMaterialProvider
{
    public Task<ProvisioningMaterial> PrepareAsync(
        SigningJob job,
        ManagedAppConfig app,
        CancellationToken cancellationToken)
    {
        throw new SigningWorkflowException(
            StableErrorCodes.AuthRequired,
            "Provisioning provider is not wired yet.");
    }
}
