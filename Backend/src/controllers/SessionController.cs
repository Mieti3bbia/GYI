using FYI.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FYI.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController(SecurityManager security) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Login(EditorCredentials input)
    {
        var actor = security.AuthenticateEditor(input.Username, input.Password);
        if (actor is null)
        {
            return Unauthorized();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, actor);
        HttpContext.User = actor;
        return Ok(new { id = security.AuthorizeEditor() });
    }

    [HttpGet]
    [Authorize(Roles = SecurityManager.EditorRole)]
    public IActionResult Current() => Ok(new { id = security.AuthorizeEditor() });

    [HttpDelete]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}

public sealed record EditorCredentials(string? Username, string? Password);
