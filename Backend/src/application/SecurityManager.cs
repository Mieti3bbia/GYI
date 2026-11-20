using FYI.Demo;

namespace FYI.Application;

public sealed class SecurityManager(IWebHostEnvironment environment)
{
    public Guid AuthorizeEditor()
    {
        // In development the server authenticates as the demo identity provisioned with the Editor role.
        if (environment.IsDevelopment())
        {
            return DemoEditor.Id;
        }
        throw new UnauthorizedAccessException();
    }
}
