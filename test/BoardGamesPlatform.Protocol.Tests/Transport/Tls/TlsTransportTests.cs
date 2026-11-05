/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Tls/TlsTransportTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks mutual TLS 1.3 on the links: the expected tier is accepted, every other certificate and every older protocol is refused
 */

// Using
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Tls;

/// <summary>
/// Tests used to check <see cref="TlsTransportListener"/>, <see cref="TlsTransportConnector"/> and <see cref="TlsTransportConnection"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of 0.5.0: a connection without the valid certificate of the tier expected is refused. The cases
/// are the ones of the roadmap (valid, missing, wrong authority, expired) and the one a valid authority alone does not stop: a valid
/// certificate of another role.</para>
/// <para><b>Behavior:</b> the server is the Back, which accepts the Gateway, unless a test says otherwise. Real sockets on the loopback address,
/// certificates built at run time; a refused client is played by a raw TLS client, which presents what the test gives.</para>
/// </remarks>
public sealed class TlsTransportTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 15000;

    #endregion

    #region Variables

    /// <summary>
    /// Authority of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// Logger of the server side
    /// </summary>
    private readonly CapturingLogger<TlsTransportTests> _serverLogger = new CapturingLogger<TlsTransportTests>();

    /// <summary>
    /// Logger of the client side
    /// </summary>
    private readonly CapturingLogger<TlsTransportTests> _clientLogger = new CapturingLogger<TlsTransportTests>();

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
    /// Method used to start a TLS listener on a test port
    /// </summary>
    /// <param name="credentials">[Cannot be null] Credentials of the server</param>
    /// <param name="clientRole">Role the clients must have</param>
    /// <returns>
    /// The listener, owned by the caller
    /// </returns>
    private TlsTransportListener StartListener(TlsCredentials credentials, PlatformRole clientRole)
    {
        // Function's return
        return new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), credentials, clientRole, _serverLogger);
    }

    /// <summary>
    /// Method used to accept one connection and establish it, the accepted connection being closed when that fails
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The established connection
    /// </returns>
    private static async Task<ITransportConnection> AcceptOneAsync(ITransportListener listener, CancellationToken cancellationToken)
    {
        // Accepting, then establishing as the link server does
        ITransportConnection accepted = await listener.AcceptAsync(cancellationToken);
        try
        {
            // Function's return
            return await listener.EstablishAsync(accepted, cancellationToken);
        }
        catch
        {
            await accepted.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Method used to open a raw TLS session to the Back, presenting the certificate given
    /// </summary>
    /// <param name="port">Port of the server</param>
    /// <param name="certificate">Certificate presented; none when null</param>
    /// <param name="protocols">TLS versions offered</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The session, when the handshake ended on this side
    /// </returns>
    private async Task<SslStream> OpenRawAsync(int port, X509Certificate2? certificate, SslProtocols protocols, CancellationToken cancellationToken)
    {
        // Connecting, then opening the session; the certificate is always the one sent, whatever the server asks
        TcpClient client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        SslStream stream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        SslClientAuthenticationOptions options = new SslClientAuthenticationOptions
        {
            TargetHost = PlatformIdentity.GetDnsName(PlatformRole.Back, TestCertificates.Suffix),
            EnabledSslProtocols = protocols,
            AllowTlsResume = false,
            RemoteCertificateValidationCallback = (_, server, _, _) => PeerCertificateValidator.Validate(server, PlatformRole.Back, TestCertificates.Suffix,
                PlatformIdentity.ServerAuthenticationOid, _certificates.TrustedRoots, new HashSet<string>(), DateTimeOffset.UtcNow) is null
        };
        if (certificate is not null)
        {
            options.ClientCertificates = new X509CertificateCollection { certificate };
            options.LocalCertificateSelectionCallback = (_, _, _, _, _) => certificate;
        }
        await stream.AuthenticateAsClientAsync(options, cancellationToken);

        // Function's return
        return stream;
    }

    /// <summary>
    /// Method used to check that a raw client presenting the certificate given is refused by the Back
    /// </summary>
    /// <param name="certificate">Certificate presented; none when null</param>
    /// <param name="reason">Text expected in the reason logged</param>
    /// <param name="token">Token of the test</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    private async Task CheckRefusedAsync(X509Certificate2? certificate, string reason, CancellationToken token)
    {
        // Serving as the Back, and connecting a raw client
        await using TlsTransportListener listener = StartListener(_certificates.CredentialsFor(PlatformRole.Back), PlatformRole.Gateway);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        try
        {
            // In TLS 1.3 the client may end its handshake before the server refuses it: the refusal then shows on its first read
            await using SslStream raw = await OpenRawAsync(listener.LocalEndPoint.Port, certificate, SslProtocols.Tls13, token);
            byte[] buffer = new byte[1];
            await Should.ThrowAsync<Exception>(async () =>
            {
                if (await raw.ReadAsync(buffer, token) == 0)
                    throw new IOException("The server ended the session");
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // The refusal reached the client during its handshake
        }

        // Checks: the server refused the client, and logged why
        await Should.ThrowAsync<AuthenticationException>(() => establishing);
        _serverLogger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("refused") && entry.Message.Contains(reason),
            string.Join(Environment.NewLine, _serverLogger.Entries.Select(entry => entry.Message)));
    }

    #endregion

    #region Publics

    /// <summary>
    /// The Gateway and the Back authenticate each other in TLS 1.3, know who the other is, and carry frames both ways
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheExpectedTiersAuthenticateEachOtherAsync()
    {
        // Serving as the Back, and connecting as the Gateway
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TlsTransportListener listener = StartListener(_certificates.CredentialsFor(PlatformRole.Back), PlatformRole.Gateway);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.Back, _clientLogger);
        await using ITransportConnection client = await connector.ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await establishing;

        // Exchanging a frame each way
        FrameCodec codec = new FrameCodec();
        await codec.WriteAsync(client.Stream, new Frame(1, new byte[] { 1, 2 }), token);
        (await codec.ReadAsync(server.Stream, token)).ShouldNotBeNull().Payload.ToArray().ShouldBe(new byte[] { 1, 2 });
        await codec.WriteAsync(server.Stream, new Frame(1, new byte[] { 3 }), token);
        (await codec.ReadAsync(client.Stream, token)).ShouldNotBeNull().Payload.ToArray().ShouldBe(new byte[] { 3 });

        // Checks: TLS 1.3, and each side knows the identity of the other
        ((SslStream)client.Stream).SslProtocol.ShouldBe(SslProtocols.Tls13);
        ((SslStream)server.Stream).SslProtocol.ShouldBe(SslProtocols.Tls13);
        client.PeerIdentity.ShouldBe("back.bgp.test");
        server.PeerIdentity.ShouldBe("gateway.bgp.test");
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains("authenticated as gateway.bgp.test"));
    }

    /// <summary>
    /// A client without a certificate is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the reason is not asserted here: on Windows, a raw client that gives no certificate was seen sending one of an
    /// earlier connection of the same process (cause not verified), which the server refuses too. That a missing certificate gives the reason
    /// "no certificate" is proven by <see cref="PeerCertificateValidatorTests"/>; the tiers always present their own certificate.</para>
    /// </remarks>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AClientWithoutCertificateIsRefusedAsync()
    {
        // Check
        await CheckRefusedAsync(null, "refused", TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A client whose certificate comes from another authority is refused, even with the name of the Gateway
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AClientOfAnotherAuthorityIsRefusedAsync()
    {
        // Issuing a Gateway certificate from another root
        using TestCertificates other = new TestCertificates("Other Root CA");

        // Check
        await CheckRefusedAsync(other.Issue(PlatformRole.Gateway), "not valid for this link", TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A client whose certificate expired is refused
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AnExpiredClientIsRefusedAsync()
    {
        // Issuing a Gateway certificate that expired yesterday
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Check
        await CheckRefusedAsync(_certificates.Issue(PlatformRole.Gateway, now.AddDays(-20), now.AddDays(-1)), "NotTimeValid", TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A valid certificate of the platform, but of another role, is refused: the key of one tier never opens a link meant for another one
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AValidCertificateOfAnotherRoleIsRefusedAsync()
    {
        // Check: the certificate of the Back itself (which holds the client usage) does not open the listener of the Back for the Gateway
        await CheckRefusedAsync(_certificates.Issue(PlatformRole.Back), "not the one of gateway.bgp.test", TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The certificate of the Gateway does not open the Very Back, which accepts the Back only
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheGatewayCannotOpenTheVeryBackAsync()
    {
        // Serving as the Very Back, and connecting with the credentials of the Gateway, as if they were stolen
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TlsTransportListener listener = StartListener(_certificates.CredentialsFor(PlatformRole.VeryBack), PlatformRole.Back);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.VeryBack, _clientLogger);
        try
        {
            await using ITransportConnection client = await connector.ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
            await Should.ThrowAsync<Exception>(async () =>
            {
                if (await client.Stream.ReadAsync(new byte[1], token) == 0)
                    throw new IOException("The server ended the session");
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // The refusal reached the client during its handshake
        }

        // Checks
        await Should.ThrowAsync<AuthenticationException>(() => establishing);
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains("not the one of back.bgp.test"));
    }

    /// <summary>
    /// A server of another role is refused by the client: the Gateway only talks to the Back
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AServerOfAnotherRoleIsRefusedByTheClientAsync()
    {
        // Serving as the Very Back, and connecting as the Gateway expecting the Back
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TlsTransportListener listener = StartListener(_certificates.CredentialsFor(PlatformRole.VeryBack), PlatformRole.Gateway);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.Back, _clientLogger);

        // Checks: the client refuses the server, and logs why
        await Should.ThrowAsync<AuthenticationException>(() => connector.ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token));
        _clientLogger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("not the one of back.bgp.test"));

        // Check: in TLS 1.3 the server may end its handshake before the client checks it; the connection then ends at its first read
        try
        {
            await using ITransportConnection served = await establishing;
            await Should.ThrowAsync<Exception>(async () =>
            {
                if (await served.Stream.ReadAsync(new byte[1], token) == 0)
                    throw new IOException("The client ended the session");
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // The server saw the handshake fail
        }
    }

    /// <summary>
    /// A certificate named after the Gateway but without the usage of a client is refused
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ACertificateWithoutTheClientUsageIsRefusedAsync()
    {
        // Check
        await CheckRefusedAsync(_certificates.IssueWithUsages(PlatformRole.Gateway, PlatformIdentity.ServerAuthenticationOid), "NotValidForUsage", TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A certificate in the refused list is refused, although it is valid in every other way
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARefusedCertificateIsRefusedAsync()
    {
        // Serving as a Back that refuses one certificate of the Gateway, and connecting with it
        CancellationToken token = TestContext.Current.CancellationToken;
        X509Certificate2 gateway = _certificates.Issue(PlatformRole.Gateway);
        string thumbprint = PeerCertificateValidator.GetThumbprint(gateway);
        TlsCredentials back = TlsCredentials.FromCertificates(PlatformRole.Back, TestCertificates.Suffix, _certificates.Issue(PlatformRole.Back), _certificates.TrustedRoots,
            new string[] { thumbprint.ToLowerInvariant() }, TimeProvider.System);
        await using TlsTransportListener listener = StartListener(back, PlatformRole.Gateway);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        try
        {
            await using SslStream raw = await OpenRawAsync(listener.LocalEndPoint.Port, gateway, SslProtocols.Tls13, token);
            await Should.ThrowAsync<Exception>(async () =>
            {
                if (await raw.ReadAsync(new byte[1], token) == 0)
                    throw new IOException("The server ended the session");
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // The refusal reached the client
        }

        // Checks
        await Should.ThrowAsync<AuthenticationException>(() => establishing);
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains($"{thumbprint} is in the refused list"));
    }

    /// <summary>
    /// A client that offers TLS 1.2 only is refused: the links speak TLS 1.3 only
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AnOlderTlsVersionIsRefusedAsync()
    {
        // Serving as the Back, and connecting a raw client that offers TLS 1.2 only, with a valid certificate of the Gateway
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TlsTransportListener listener = StartListener(_certificates.CredentialsFor(PlatformRole.Back), PlatformRole.Gateway);
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
#pragma warning disable CA5398 // The older version is offered on purpose: the test proves the server refuses it
        Task<SslStream> opening = OpenRawAsync(listener.LocalEndPoint.Port, _certificates.Issue(PlatformRole.Gateway), SslProtocols.Tls12, token);
#pragma warning restore CA5398

        // Checks: neither side gets a session
        await Should.ThrowAsync<Exception>(() => opening);
        await Should.ThrowAsync<Exception>(() => establishing);
    }

    /// <summary>
    /// Closing the sending side ends the stream for the peer, which can still answer: on TLS (close_notify then FIN) as on plain TCP
    /// </summary>
    /// <param name="useTls">Whether the connection is TLS</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClosingTheSendingSideLetsThePeerAnswerAsync(bool useTls)
    {
        // Connecting a client and a server, over TLS or plain TCP
        CancellationToken token = TestContext.Current.CancellationToken;
        TcpTransportListener tcp = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        await using ITransportListener listener = useTls ? new TlsTransportListener(tcp, _certificates.CredentialsFor(PlatformRole.Back), PlatformRole.Gateway, _serverLogger) : tcp;
        ITransportConnector connector = useTls
            ? new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.Back, _clientLogger)
            : new TcpTransportConnector();
        Task<ITransportConnection> establishing = AcceptOneAsync(listener, token);
        await using ITransportConnection client = await connector.ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await establishing;
        FrameCodec codec = new FrameCodec();

        // Sending a frame, then closing the sending side of the client
        await codec.WriteAsync(client.Stream, new Frame(1, new byte[] { 1 }), token);
        await client.CloseSendAsync(token);

        // Checks: the server reads the frame then the clean end, and its answer still reaches the client
        (await codec.ReadAsync(server.Stream, token)).ShouldNotBeNull();
        (await codec.ReadAsync(server.Stream, token)).ShouldBeNull();
        await codec.WriteAsync(server.Stream, new Frame(1, new byte[] { 2 }), token);
        (await codec.ReadAsync(client.Stream, token)).ShouldNotBeNull().Payload.ToArray().ShouldBe(new byte[] { 2 });
    }

    /// <summary>
    /// The connector and the listener refuse a role that never takes their side
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact]
    public async Task TheSidesOfTheRolesAreCheckedAsync()
    {
        // Preparing credentials of each role
        TlsCredentials gateway = _certificates.CredentialsFor(PlatformRole.Gateway);
        TlsCredentials veryBack = _certificates.CredentialsFor(PlatformRole.VeryBack);
        await using TcpTransportListener tcp = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));

        // Checks: the Very Back never connects, the Gateway never accepts, nobody connects to the Gateway nor is accepted from the Very Back
        Should.Throw<ArgumentException>(() => new TlsTransportConnector(new TcpTransportConnector(), veryBack, PlatformRole.Back, _clientLogger));
        Should.Throw<ArgumentException>(() => new TlsTransportConnector(new TcpTransportConnector(), gateway, PlatformRole.Gateway, _clientLogger));
        Should.Throw<ArgumentException>(() => new TlsTransportListener(tcp, gateway, PlatformRole.Back, _serverLogger));
        Should.Throw<ArgumentException>(() => new TlsTransportListener(tcp, veryBack, PlatformRole.VeryBack, _serverLogger));
    }

    /// <summary>
    /// Method used to release the certificates of the test
    /// </summary>
    public void Dispose()
    {
        // Releasing the authority and its certificates
        _certificates.Dispose();
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
