/*
 *	@file	test/BoardGamesPlatform.Back.Tests/BackTlsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the done criterion of 0.5.0 on the Back started as delivered: only the Gateway, with its certificate, is served
 */

// Using
using System.Globalization;
using System.Security.Authentication;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests;

/// <summary>
/// Tests used to check mutual TLS on the Back started with the registrations of Program.cs
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of 0.5.0, on a real tier: a connection without the valid certificate of the Gateway is refused, and a
/// Back without a valid certificate of its own does not start.</para>
/// <para><b>Behavior:</b> the Back listens on a test port, with the certificates of <see cref="TestTlsFiles"/>; its Very Back is not running
/// (the Ping then answers a code, which is enough to prove that the request was served).</para>
/// </remarks>
public sealed class BackTlsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 20000;

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

    /// <summary>
    /// Method used to build a host of the Back as Program.cs does, on a test port
    /// </summary>
    /// <param name="port">Port the Back listens on</param>
    /// <param name="tlsSettings">[Cannot be null] "Tls" settings of the host</param>
    /// <returns>
    /// The host, not started
    /// </returns>
    private static IHost BuildHost(int port, Dictionary<string, string?> tlsSettings)
    {
        // Building the host with the registrations of Program.cs; the Very Back is a port nobody listens on
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        Dictionary<string, string?> settings = new Dictionary<string, string?>(tlsSettings)
        {
            ["Back:ListenAddress"] = "127.0.0.1",
            ["Back:Port"] = port.ToString(CultureInfo.InvariantCulture),
            ["VeryBack:Host"] = "127.0.0.1",
            ["VeryBack:Port"] = TestPorts.Block.Next().ToString(CultureInfo.InvariantCulture),
        };
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddLogging();
        builder.AddPlatformBack();

        // Function's return
        return builder.Build();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The Gateway is served; the certificate of another role of the platform is refused before any request
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task OnlyTheGatewayIsServedAsync()
    {
        // Starting the Back
        CancellationToken token = TestContext.Current.CancellationToken;
        int port = TestPorts.Block.Next();
        using IHost host = BuildHost(port, TestTlsFiles.Shared.GetSettings(PlatformRole.Back));
        await host.StartAsync(token);
        try
        {
            // Check: the Gateway opens a session and its request is served (the Very Back is down: the answer is a code)
            await using (RequestClient gateway = await TestRequestClients.ConnectAsync("127.0.0.1", port, TestTlsFiles.Shared.CreateConnector(PlatformRole.Gateway, PlatformRole.Back), token))
            {
                RemoteErrorException answer = await Should.ThrowAsync<RemoteErrorException>(() => gateway.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));
                answer.Code.ShouldBe(ErrorCodes.LinkUnavailable);
            }

            // Check: the Back itself, with its valid certificate of the platform, is refused as a client of the Back
            ITransportConnector notTheGateway = TestTlsFiles.Shared.CreateConnector(PlatformRole.Back, PlatformRole.Back);
            Exception refused = await Should.ThrowAsync<Exception>(async () =>
            {
                await using RequestClient client = await TestRequestClients.ConnectAsync("127.0.0.1", port, notTheGateway, token);
            });
            (refused is AuthenticationException or IOException).ShouldBeTrue(refused.ToString());
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A Back without its TLS settings, or with a certificate of another role, does not start, and nothing listens
    /// </summary>
    [Fact]
    public void AnInvalidCertificateStopsTheStartup()
    {
        // Building a Back without TLS settings, then with the certificate of the Gateway
        using IHost withoutSettings = BuildHost(TestPorts.Block.Next(), new Dictionary<string, string?>());
        Dictionary<string, string?> gatewayCertificate = TestTlsFiles.Shared.GetSettings(PlatformRole.Back);
        gatewayCertificate[$"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePath)}"] = TestTlsFiles.Shared.GetCertificatePath(PlatformRole.Gateway);
        using IHost withAnotherRole = BuildHost(TestPorts.Block.Next(), gatewayCertificate);

        // Checks: building the listener fails, naming the settings, or why the certificate cannot be used; the password never appears
        OptionsValidationException missing = Should.Throw<OptionsValidationException>(() => withoutSettings.Services.GetServices<IHostedService>().OfType<BackListenerService>().ToArray());
        missing.Message.ShouldContain("Tls:CertificatePath");
        missing.Message.ShouldContain("Tls:CertificatePassword");
        InvalidOperationException wrongRole = Should.Throw<InvalidOperationException>(() => withAnotherRole.Services.GetServices<IHostedService>().OfType<BackListenerService>().ToArray());
        wrongRole.Message.ShouldContain("cannot be used");
        wrongRole.Message.ShouldNotContain(TestTlsFiles.Password);
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
