using RestrictPoint.Api.Identity.Domain;
using Xunit;

namespace RestrictPoint.Api.Identity.Tests.Domain;

public sealed class SlugTests
{
    [Theory]
    [InlineData("Contoso Software", "contoso-software")]
    [InlineData("  Acme, Inc.  ", "acme-inc")]
    [InlineData("Über Apps GmbH", "ber-apps-gmbh")]
    [InlineData("a", "a")]
    [InlineData("Already-Slugged", "already-slugged")]
    public void FromName_derives_url_safe_slug(string name, string expected)
    {
        Assert.Equal(expected, Slug.FromName(name));
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("   ")]
    public void FromName_returns_null_for_unusable_names(string name)
    {
        Assert.Null(Slug.FromName(name));
    }

    [Fact]
    public void FromName_truncates_to_max_length()
    {
        var slug = Slug.FromName(new string('a', 500));

        Assert.NotNull(slug);
        Assert.True(slug.Length <= Slug.MaxLength);
        Assert.True(Slug.IsValid(slug));
    }

    [Fact]
    public void WithUniquenessSuffix_stays_within_max_length_and_valid()
    {
        var baseSlug = Slug.FromName(new string('b', 500))!;

        var suffixed = Slug.WithUniquenessSuffix(baseSlug);

        Assert.True(suffixed.Length <= Slug.MaxLength);
        Assert.True(Slug.IsValid(suffixed));
        Assert.NotEqual(baseSlug, suffixed);
    }

    [Theory]
    [InlineData("valid-slug", true)]
    [InlineData("valid123", true)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("UPPER", false)]
    [InlineData("", false)]
    public void IsValid_enforces_slug_grammar(string slug, bool expected)
    {
        Assert.Equal(expected, Slug.IsValid(slug));
    }
}

public sealed class PoliciesTests
{
    [Theory]
    [InlineData(OrganizationRole.Owner, true)]
    [InlineData(OrganizationRole.Admin, true)]
    [InlineData(OrganizationRole.Developer, false)]
    [InlineData(OrganizationRole.Billing, false)]
    [InlineData(OrganizationRole.Support, false)]
    [InlineData(OrganizationRole.ReadOnly, false)]
    public void CanManageMembers_grants_owner_and_admin_only(OrganizationRole role, bool expected)
    {
        Assert.Equal(expected, Policies.Grants(Policies.CanManageMembers, role));
    }

    [Fact]
    public void All_roles_can_view_organization()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.True(Policies.Grants(Policies.CanViewOrganization, role));
        }
    }

    [Fact]
    public void Unknown_policy_throws()
    {
        Assert.Throws<ArgumentException>(() => Policies.Grants("NoSuchPolicy", OrganizationRole.Owner));
    }
}

public sealed class OrganizationRoleTests
{
    [Theory]
    [InlineData("Admin", OrganizationRole.Admin)]
    [InlineData("admin", OrganizationRole.Admin)]
    [InlineData("READONLY", OrganizationRole.ReadOnly)]
    public void TryParse_is_case_insensitive(string input, OrganizationRole expected)
    {
        Assert.True(OrganizationRoleExtensions.TryParse(input, out var role));
        Assert.Equal(expected, role);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("7")]
    public void TryParse_rejects_unknown_roles(string? input)
    {
        Assert.False(OrganizationRoleExtensions.TryParse(input, out _));
    }
}
