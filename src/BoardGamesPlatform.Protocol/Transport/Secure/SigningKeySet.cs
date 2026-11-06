/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SigningKeySet.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Snapshot of the keys of a tier for one handshake: its signing key and the pinned keys of its peer
 */

// Using
using System.Security.Cryptography;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Keys one handshake uses, taken once before it starts
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a reload must never change the keys in the middle of a handshake; each connection takes one immutable snapshot.</para>
/// <para><b>Behavior:</b> the set checks itself when it is built (see <see cref="Create"/>): its own key on P-256 with a private part, 1 to
/// <see cref="MaxPeerKeys"/> pinned keys, no duplicate, not its own key. Its generation grows with each reload of the keys, so that the end of
/// a handshake can tell whether the list changed meanwhile. A set is never disposed while a connection may still use it: its signing key is
/// released by the garbage collector.</para>
/// </remarks>
public sealed class SigningKeySet
{
    #region Members

    #region Constants

    /// <summary>
    /// Largest number of pinned keys of the peer
    /// </summary>
    public const int MaxPeerKeys = 4;

    #endregion

    #region Variables

    /// <summary>
    /// Signing key of the tier, with its private part
    /// </summary>
    private readonly ECDsa _signingKey;

    /// <summary>
    /// Lock of the signatures
    /// </summary>
    private readonly Lock _signLock = new Lock();

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

    /// <summary>
    /// Public part of the signing key of the tier
    /// </summary>
    public SigningPublicKey OwnKey { get; }

    /// <summary>
    /// Pinned keys of the peer
    /// </summary>
    public IReadOnlyList<SigningPublicKey> PeerKeys { get; }

    /// <summary>
    /// Generation of the keys: grows with each reload taken
    /// </summary>
    public long Generation { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="signingKey">Signing key, checked</param>
    /// <param name="ownKey">Its public part</param>
    /// <param name="peerKeys">Pinned keys, checked</param>
    /// <param name="generation">Generation of the keys</param>
    private SigningKeySet(ECDsa signingKey, SigningPublicKey ownKey, IReadOnlyList<SigningPublicKey> peerKeys, long generation)
    {
        // Setting
        _signingKey = signingKey;
        OwnKey = ownKey;
        PeerKeys = peerKeys;
        Generation = generation;
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

    /// <summary>
    /// Method used to build a set from keys already loaded
    /// </summary>
    /// <param name="signingKey">[Cannot be null] Signing key of the tier, with its private part; the set takes ownership of it</param>
    /// <param name="peerKeys">[Cannot be null] Pinned keys of the peer</param>
    /// <param name="generation">Generation of the keys</param>
    /// <returns>
    /// The set
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="InvalidOperationException">If the signing key is not a private key of P-256, or the pinned keys are not 1 to
    /// <see cref="MaxPeerKeys"/> distinct keys other than the key of the tier</exception>
    public static SigningKeySet Create(ECDsa signingKey, IEnumerable<SigningPublicKey> peerKeys, long generation)
    {
        // Checks
        if (signingKey is null)
            throw new ArgumentNullException(nameof(signingKey), $"The value of the parameter: {nameof(signingKey)} cannot be null");
        if (peerKeys is null)
            throw new ArgumentNullException(nameof(peerKeys), $"The value of the parameter: {nameof(peerKeys)} cannot be null");

        // Checking the signing key: a private key of P-256
        SigningPublicKey ownKey;
        try
        {
            ECParameters parameters = signingKey.ExportParameters(includePrivateParameters: true);
            CryptographicOperations.ZeroMemory(parameters.D);
            ownKey = SigningPublicKey.FromKey(signingKey);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The signing key is not a private key of the curve P-256", exception);
        }

        // Checking the pinned keys: 1 to 4, distinct, never the key of the tier itself (a reflected key)
        List<SigningPublicKey> peers = peerKeys.ToList();
        if (peers.Count == 0 || peers.Count > MaxPeerKeys)
            throw new InvalidOperationException($"The public keys of the peer must be 1 to {MaxPeerKeys} keys; {peers.Count} given");
        if (peers.Distinct().Count() != peers.Count)
            throw new InvalidOperationException("The public keys of the peer hold the same key twice");
        if (peers.Contains(ownKey))
            throw new InvalidOperationException("The public keys of the peer hold the key of this tier");

        // Function's return
        return new SigningKeySet(signingKey, ownKey, peers.AsReadOnly(), generation);
    }

    /// <summary>
    /// Method used to find the pinned key named by a key id
    /// </summary>
    /// <param name="keyId">Key id received</param>
    /// <returns>
    /// The pinned key, or null when none has that key id
    /// </returns>
    public SigningPublicKey? FindPeerKey(ReadOnlySpan<byte> keyId)
    {
        // Comparing each pinned key, every comparison in constant time
        SigningPublicKey? found = null;
        foreach (SigningPublicKey key in PeerKeys)
        {
            if (key.HasKeyId(keyId))
                found = key;
        }

        // Function's return
        return found;
    }

    /// <summary>
    /// Method used to tell whether a pinned key is still in this set
    /// </summary>
    /// <param name="key">[Cannot be null] Key of a peer</param>
    /// <returns>
    /// True when the key is pinned in this set
    /// </returns>
    public bool HasPeerKey(SigningPublicKey key)
    {
        // Function's return
        return PeerKeys.Contains(key);
    }

    /// <summary>
    /// Method used to sign with the signing key of the tier
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>
    /// The signature, ECDSA with SHA-256 in the IEEE P1363 format
    /// </returns>
    internal byte[] Sign(ReadOnlySpan<byte> data)
    {
        // One signature at a time: several handshakes share the key, and an instance of ECDsa is not documented as thread-safe
        lock (_signLock)
        {
            // Function's return
            return _signingKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
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
