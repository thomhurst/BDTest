using System.Security.Claims;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class UserPersonalizerTests
{
    [TestCase(true, "Alex Morgan", "Alex Morgan")]
    [TestCase(true, null, null)]
    [TestCase(false, "Anonymous name", null)]
    [TestCase(false, null, null)]
    public void OnlyAuthenticatedUserNamesAreShown(bool authenticated, string name, string expected)
    {
        var claims = name == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Name, name) };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
        };

        Assert.That(new UserPersonalizer().GetNameOfUser(context), Is.EqualTo(expected));
    }

    [Test]
    public void ConfiguredNameClaimTypeIsRespected()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("name", "Jordan Lee") }, "test", "name", ClaimTypes.Role))
        };

        Assert.That(new UserPersonalizer().GetNameOfUser(context), Is.EqualTo("Jordan Lee"));
    }
}
