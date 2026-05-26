namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Default scoped implementation of <see cref="ITenantContext"/>. Write-once per scope —
/// reading before <c>SetTenantId</c> throws to surface pipeline-ordering mistakes loudly.
/// In Blazor Server, the SignalR circuit gets its own DI scope that doesn't see request
/// middleware; a host-side bridge (e.g. <c>TenantStateBridge</c>) is responsible for
/// hydrating this context inside the circuit.
/// </summary>
public class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string TenantId =>
        !IsResolved
            ? throw new InvalidOperationException("TenantId accessed before resolution. Ensure TenantResolutionMiddleware ran for this request.")
            : _tenantId!;

    public bool IsResolved { get; private set; }

    public bool IsAdminTenant => TenantId == "admin";

    public void SetTenantId(string tenantId)
    {
        if (IsResolved)
        {
            if (string.Equals(_tenantId, tenantId, StringComparison.Ordinal))
                return;

            throw new InvalidOperationException("TenantId already resolved with a different value.");
        }

        _tenantId = tenantId;
        IsResolved = true;
    }
}
