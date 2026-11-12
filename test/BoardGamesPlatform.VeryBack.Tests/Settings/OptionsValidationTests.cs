/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Settings/OptionsValidationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the validation of the settings of the Very Back
 */

// Using
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Settings;

/// <summary>
/// Tests used to check the validators of the "VeryBack" and "ConnectionStrings" settings
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Very Back refuses to start with an unusable endpoint or without a connection string (fail closed).</para>
/// <para><b>Behavior:</b> the validators are called directly; the host calls them at startup through <c>ValidateOnStart</c>.</para>
/// </remarks>
public sealed class OptionsValidationTests
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

    #region VeryBack

    /// <summary>
    /// The defaults listen on the loopback address, port 7822, and are valid
    /// </summary>
    [Fact]
    public void TheDefaultsAreTheLoopbackAddressAndPort7822()
    {
        // Checks
        VeryBackOptions options = new VeryBackOptions();
        options.ListenAddress.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(7822);
        new VeryBackOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// IPv4 and IPv6 addresses and the limits of the port range are accepted
    /// </summary>
    [Theory]
    [InlineData("127.0.0.1", 1)]
    [InlineData("0.0.0.0", 65535)]
    [InlineData("::1", 7822)]
    public void ValidEndpointsAreAccepted(string address, int port)
    {
        // Check
        VeryBackOptions options = new VeryBackOptions { ListenAddress = address, Port = port };
        new VeryBackOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// An address that is not an IP address is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]
    [InlineData("300.1.1.1")]
    public void AnInvalidAddressIsRefused(string address)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new VeryBackOptionsValidator().Validate(null, new VeryBackOptions { ListenAddress = address });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VeryBack:ListenAddress");
    }

    /// <summary>
    /// A port outside 1-65535 is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void APortOutsideTheRangeIsRefused(int port)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new VeryBackOptionsValidator().Validate(null, new VeryBackOptions { Port = port });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VeryBack:Port");
    }

    #endregion

    #region ConnectionStrings

    /// <summary>
    /// A missing, empty or blank main connection string is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingConnectionStringIsRefused(string? connectionString)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new ConnectionStringsOptionsValidator().Validate(null, new ConnectionStringsOptions { Main = connectionString });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionStrings:Main");
    }

    /// <summary>
    /// A connection string the SQL client reads is accepted, including a quoted value holding a semicolon
    /// </summary>
    /// <param name="connectionString">Valid connection string</param>
    [Theory]
    [InlineData("Server=localhost;Database=X")]
    [InlineData("Server=localhost;Database=X;User Id=app;Password=\"ab;cd\"")]
    [InlineData("Server=localhost;Database=X;User Id=app;Password='ab;cd'")]
    public void AConnectionStringIsAccepted(string connectionString)
    {
        // Check
        new ConnectionStringsOptionsValidator().Validate(null, new ConnectionStringsOptions { Main = connectionString }).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// The risk is real: the error of the SQL client on a malformed string quotes the unknown keyword, here a piece of a password holding
    /// a semicolon
    /// </summary>
    [Fact]
    public void TheErrorOfTheSqlClientQuotesAPieceOfTheString()
    {
        // Check
        ArgumentException exception = Should.Throw<ArgumentException>(() => new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Password=ab;Kx9secret=cd"));
        exception.Message.ShouldContain("Kx9secret", Case.Insensitive);
    }

    /// <summary>
    /// A connection string the SQL client cannot read is refused at startup, and the message repeats nothing of it
    /// </summary>
    /// <param name="connectionString">Malformed connection string, holding the text "Kx9secret"</param>
    [Theory]
    [InlineData("Server=localhost;Password=ab;Kx9secret=cd")]
    [InlineData("Server=localhost;Password='Kx9secret")]
    [InlineData("Server=localhost;Connect Timeout=Kx9secret")]
    [InlineData("Server=localhost;Encrypt=Kx9secret")]
    public void AnUnreadableConnectionStringIsRefusedWithoutRepeatingIt(string connectionString)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new ConnectionStringsOptionsValidator().Validate(null, new ConnectionStringsOptions { Main = connectionString });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionStrings:Main");
        result.FailureMessage.ShouldNotContain("Kx9secret", Case.Insensitive);
    }

    /// <summary>
    /// Outside Development, a connection that is not encrypted, or that trusts the certificate of a remote server, is refused at startup, and the
    /// message repeats nothing of the string
    /// </summary>
    /// <param name="connectionString">Connection string holding the text "Kx9secret" as its password</param>
    [Theory]
    [InlineData("Server=sql01;Database=X;User Id=app;Password=Kx9secret;Encrypt=False")]
    [InlineData("Server=sql01;Database=X;User Id=app;Password=Kx9secret;Encrypt=Optional")]
    [InlineData("Server=sql01;Database=X;User Id=app;Password=Kx9secret;TrustServerCertificate=True")]
    [InlineData("Server=tcp:sql01,1433;Database=X;User Id=app;Password=Kx9secret;TrustServerCertificate=True")]
    public void AnUnprotectedConnectionIsRefusedOnAServer(string connectionString)
    {
        // Validating with the rules of a server, then of a developer machine
        Microsoft.Extensions.Options.ValidateOptionsResult server = new ConnectionStringsOptionsValidator(new HostingEnvironment { EnvironmentName = Environments.Production })
            .Validate(null, new ConnectionStringsOptions { Main = connectionString });
        Microsoft.Extensions.Options.ValidateOptionsResult developer = new ConnectionStringsOptionsValidator(new HostingEnvironment { EnvironmentName = Environments.Development })
            .Validate(null, new ConnectionStringsOptions { Main = connectionString });

        // Checks
        server.Failed.ShouldBeTrue();
        server.FailureMessage.ShouldContain("ConnectionStrings:Main");
        server.FailureMessage.ShouldNotContain("Kx9secret", Case.Insensitive);
        developer.Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Outside Development, an encrypted connection is accepted, and so is the trust of the self-signed certificate of the local SQL Server
    /// </summary>
    /// <param name="connectionString">Connection string</param>
    [Theory]
    [InlineData("Server=sql01;Database=X;User Id=app;Password=p")]
    [InlineData("Server=sql01;Database=X;User Id=app;Password=p;Encrypt=Strict")]
    [InlineData("Server=localhost;Database=X;Integrated Security=True;TrustServerCertificate=True")]
    [InlineData("Server=tcp:127.0.0.1,1433;Database=X;Integrated Security=True;TrustServerCertificate=True")]
    [InlineData("Server=.\\SQLEXPRESS;Database=X;Integrated Security=True;TrustServerCertificate=True")]
    public void AnEncryptedConnectionIsAcceptedOnAServer(string connectionString)
    {
        // Check
        new ConnectionStringsOptionsValidator(new HostingEnvironment { EnvironmentName = Environments.Production })
            .Validate(null, new ConnectionStringsOptions { Main = connectionString }).Succeeded.ShouldBeTrue();
    }

    #endregion

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
