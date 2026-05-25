namespace CrewJet.Server.Features.Identity.TenantResolution;

/// <summary>
/// Default scoped implementation of <see cref="ITenantContext"/>. Write-once per request —
/// double-set or pre-resolution read throw to surface pipeline-ordering mistakes loudly.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "TenantId accessed before resolution. Ensure TenantResolutionMiddleware ran for this request.");

    public bool IsResolved => _tenantId is not null;

    public void SetTenantId(string tenantId)
    {
        if (_tenantId is not null)
            throw new InvalidOperationException("TenantId has already been set for this request.");

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId cannot be null or whitespace.", nameof(tenantId));

        _tenantId = tenantId;
    }
}
