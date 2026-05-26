namespace CrewJet.Shared.Features.Identity;

/// <summary>
/// Strongly typed tenant identifier.
/// </summary>
/// <param name="Value"></param>
public readonly record struct TenantId(Guid Value)
{
    public Guid ToGuid() => Value;
    public override string ToString() => ToGuid().ToString();

    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId From(Guid id) => new(id);

    /// <summary>
    /// Non-throwing parse. Returns <c>true</c> and a populated <paramref name="tenantId"/>
    /// on success. Use this when the input may legitimately be absent or invalid —
    /// e.g. a claim that hasn't been minted yet, or a stale cookie that pre-dates
    /// the strongly-typed Id rename.
    /// </summary>
    public static bool TryFrom(string? id, out TenantId tenantId)
    {
        if (Guid.TryParse(id, out var guid))
        {
            tenantId = new TenantId(guid);
            return true;
        }

        tenantId = default;
        return false;
    }
}

/// <summary>
/// Lightweight platform-level tenant document.
/// Created at company registration time together with the first UserProfile.
/// Lives in the Marten database under its own tenant_id (the slug).
/// The domain model is intentionally tenant-unaware; this document is
/// primarily for platform/SuperAdmin operations and future per-tenant
/// configuration (IdP settings, branding, etc.).
/// </summary>
public record Tenant
{
    /// <summary>
    /// The tenant identifier.
    /// This is also the value used for Marten's tenant_id column on all documents belonging to this tenant.
    /// </summary>
    public TenantId Id { get; init; } = TenantId.New();

    /// <summary>
    /// Human-friendly company / organization name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The sanitized subdomain slug, e.g. "acme-electric").
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// Lifecycle status. Common values: "Active", "Suspended", "PendingActivation".
    /// </summary>
    public string Status { get; set; } = "Active";

    /// Create/update deets
    public Guid? CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    // Future expansion points (kept minimal for now):
    // public string? PrimaryContactEmail { get; set; }
    // public string? TimeZone { get; set; }
    // public string? Locale { get; set; }
    // public TenantIdpConfig? IdpConfig { get; set; }   // Phase 2 per-tenant federation settings
}
