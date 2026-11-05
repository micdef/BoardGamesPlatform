/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Tls/PlatformIdentityTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the names and the sides of the roles written in the certificates of the platform
 */

// Using
using BoardGamesPlatform.Protocol.Transport.Tls;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Tls;

/// <summary>
/// Tests used to check <see cref="PlatformIdentity"/>
/// </summary>
public sealed class PlatformIdentityTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Enumerations

    // #N/A

    #endregion

    #region Services

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members & Properties

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// Each role has its name, which reads back to the same role, and its sides on the links
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="name">Name expected</param>
    /// <param name="isServer">Whether the role accepts connections</param>
    /// <param name="isClient">Whether the role opens connections</param>
    [Theory]
    [InlineData(PlatformRole.Gateway, "gateway", false, true)]
    [InlineData(PlatformRole.Back, "back", true, true)]
    [InlineData(PlatformRole.VeryBack, "veryback", true, false)]
    public void EachRoleHasItsNameAndItsSides(PlatformRole role, string name, bool isServer, bool isClient)
    {
        // Checks
        PlatformIdentity.GetRoleName(role).ShouldBe(name);
        PlatformIdentity.TryParseRole(name, out PlatformRole read).ShouldBeTrue();
        read.ShouldBe(role);
        PlatformIdentity.GetDnsName(role, "bgp.internal").ShouldBe($"{name}.bgp.internal");
        PlatformIdentity.IsServer(role).ShouldBe(isServer);
        PlatformIdentity.IsClient(role).ShouldBe(isClient);
    }

    /// <summary>
    /// Only the exact names of the roles are read, in lower case
    /// </summary>
    /// <param name="name">Text to read</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Back")]
    [InlineData("very-back")]
    [InlineData("admin")]
    public void OtherNamesAreNotRoles(string? name)
    {
        // Check
        PlatformIdentity.TryParseRole(name, out _).ShouldBeFalse();
    }

    /// <summary>
    /// A suffix is DNS labels of lower-case letters, digits and inner hyphens
    /// </summary>
    /// <param name="suffix">Suffix to check</param>
    /// <param name="isValid">Whether it is valid</param>
    [Theory]
    [InlineData("bgp.internal", true)]
    [InlineData("lab", true)]
    [InlineData("home-lab.example.be", true)]
    [InlineData("a1.b2", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Bgp.internal", false)]
    [InlineData("bgp..internal", false)]
    [InlineData(".bgp", false)]
    [InlineData("bgp.", false)]
    [InlineData("-bgp.internal", false)]
    [InlineData("bgp-.internal", false)]
    [InlineData("bgp_internal", false)]
    [InlineData("*.bgp", false)]
    [InlineData("bgp internal", false)]
    [InlineData("bgp.internal\n", false)]
    public void ASuffixIsChecked(string? suffix, bool isValid)
    {
        // Check
        PlatformIdentity.IsValidSuffix(suffix).ShouldBe(isValid);
    }

    /// <summary>
    /// A suffix longer than the maximum, or a label longer than 63 characters, is refused, and an invalid suffix never builds a name
    /// </summary>
    [Fact]
    public void ALongSuffixIsRefused()
    {
        // Checks
        PlatformIdentity.IsValidSuffix(new string('a', 63)).ShouldBeTrue();
        PlatformIdentity.IsValidSuffix(new string('a', 64)).ShouldBeFalse();
        PlatformIdentity.IsValidSuffix(string.Join('.', Enumerable.Repeat("abcdefghi", 21))).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => PlatformIdentity.GetDnsName(PlatformRole.Back, "Not Valid"));
        Should.Throw<ArgumentOutOfRangeException>(() => PlatformIdentity.GetRoleName((PlatformRole)99));
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    // #N/A

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
