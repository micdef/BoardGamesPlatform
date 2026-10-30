/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/ProbingHandler.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	HTTP handler that applies a connection probe before sending a request to the in-process Gateway
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// HTTP handler placed in front of the in-memory handler of the test server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> carries the <see cref="ConnectionProbe"/> to the requests of a SignalR connection.</para>
/// <para><b>Behavior:</b> asks the probe, then sends the request to the inner handler.</para>
/// </remarks>
public sealed class ProbingHandler : DelegatingHandler
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Probe applied to the requests
    /// </summary>
    private readonly ConnectionProbe _probe;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] Handler that sends the requests to the test server</param>
    /// <param name="probe">[Cannot be null] Probe applied to the requests</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public ProbingHandler(HttpMessageHandler inner, ConnectionProbe probe)
        : base(inner)
    {
        // Check
        if (probe is null)
            throw new ArgumentNullException(nameof(probe), $"The value of the parameter: {nameof(probe)} cannot be null");

        // Setting
        _probe = probe;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    // #N/A

    #endregion

    #region Publics

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Applying the probe, then sending the request
        await _probe.BeforeSendAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
