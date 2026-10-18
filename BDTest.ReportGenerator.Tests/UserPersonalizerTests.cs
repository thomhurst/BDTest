using System.Security.Claims;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Http;

namespace BDTest.ReportGenerator.Tests;

public class UserPersonalizerTests
{
    [Test]
    [Arguments(true, "Alex Morgan", "Alex Morgan")]
    [Arguments(true, null, null)]
    [Arguments(false, "Anonymous name", null)]
    [Arguments(false, null, null)]
    public async Task OnlyAuthenticatedUserNamesAreShown(bool authenticated, string? name, string? expected)
    {
        var claims = name == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Name, name) };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
        };

        await Assert.That(new UserPersonalizer().GetNameOfUser(context)).IsEqualTo(expected);
    }

    [Test]
    public async Task ConfiguredNameClaimTypeIsRespected()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("name", "Jordan Lee") }, "test", "name", ClaimTypes.Role))
        };

        await Assert.That(new UserPersonalizer().GetNameOfUser(context)).IsEqualTo("Jordan Lee");
    }
}
