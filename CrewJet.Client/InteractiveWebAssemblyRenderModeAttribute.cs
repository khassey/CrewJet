using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CrewJet.Client;

/// <summary>
/// Convenience attribute that applies <see cref="RenderMode.InteractiveWebAssembly"/>
/// to a routable component. Used via <c>@attribute</c> in <c>Pages/_Imports.razor</c>
/// so every client page defaults to interactive WASM without each file restating it.
///
/// Blazor's host renderer reads <see cref="RenderModeAttribute"/> via
/// <c>GetCustomAttribute(inherit: true)</c>, so applying this through _Imports
/// (which compiles into each generated component class) is equivalent to writing
/// <c>@rendermode InteractiveWebAssembly</c> at the top of every page.
/// </summary>
public sealed class InteractiveWebAssemblyRenderModeAttribute : RenderModeAttribute
{
    public override IComponentRenderMode Mode => RenderMode.InteractiveWebAssembly;
}
