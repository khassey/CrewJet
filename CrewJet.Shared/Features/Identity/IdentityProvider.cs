namespace CrewJet.Shared.Features.Identity;

public enum IdentityProvider
{
    Local,
    Microsoft365,
    Azure,
    Okta,
}

public enum ProvisioningSource
{
    SelfRegistration,
    IdentityManagement,
}
