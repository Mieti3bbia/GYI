using System.Security.Claims;
using FYI.Demo;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FYI.Application;

public sealed class SecurityManager(
    IWebHostEnvironment environment, IConfiguration configuration, IHttpContextAccessor httpContext)
{
    public const string EditorRole = "Editor";

    public ClaimsPrincipal? AuthenticateEditor(string? username, string? password)
    {
        if (!environment.IsDevelopment() || username != "editor" ||
            password != (configuration["Demo:EditorPassword"] ?? "editor"))
        {
            return null;
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, DemoEditor.Id.ToString()),
            new Claim(ClaimTypes.Role, EditorRole)
        ], CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public Guid AuthorizeEditor()
    {
        var actor = httpContext.HttpContext?.User;
        if (actor?.IsInRole(EditorRole) == true &&
            Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return id;
        }
        throw new UnauthorizedAccessException();
    }
}
