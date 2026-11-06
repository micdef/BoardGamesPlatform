/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/Vectors/SecureChannelVectors.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Test vectors of the secure channel, computed by the Python package "cryptography", never by the code under test
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors;

/// <summary>
/// Values printed by <c>generate_vectors.py</c> of this folder, in upper-case hexadecimal
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a vector computed by the code under test only proves that the code agrees with itself; these come from another
/// implementation of P-256 (OpenSSL through the Python package), and an HKDF written from RFC 5869 with the standard library of Python.</para>
/// </remarks>
internal static class SecureChannelVectors
{
    #region Members

    #region Constants

    /// <summary>
    /// Private scalar of the ephemeral key of the client
    /// </summary>
    public const string ClientEphemeralPrivate = "DA8D66003F84E5CE243AEF12C0007AF5ACB440F7652244E8700A106F9A359913";

    /// <summary>
    /// Private scalar of the ephemeral key of the server
    /// </summary>
    public const string ServerEphemeralPrivate = "531A984355EEBB6860EDFAA136C5700357D56D72025A83021BFA385EBE7FC37E";

    /// <summary>
    /// Point of the ephemeral key of the client, uncompressed
    /// </summary>
    public const string ClientEphemeralPoint = "04F00789F9B936F74AE1EF16E3137AEB7CB72DB8210B39D0BBE592360E2261FF08D6B183DE199F1F8BDECC216001CCF92CE5FB553CA9F5CBEB697BB400803E1BD9";

    /// <summary>
    /// Point of the ephemeral key of the server, uncompressed
    /// </summary>
    public const string ServerEphemeralPoint = "04831052149F627BC5BA2ED704934EF4CFB9EDE4478DF29981924AC5AE1148C7B65EC0FACFE66FB6E525028CC1DFAB06829854CE2B8616AFADFFD33A7CD449FA1D";

    /// <summary>
    /// Raw ECDH secret of the two ephemeral keys
    /// </summary>
    public const string SharedSecret = "EF635BEC7AF3D3C613788122FDB0D82D5142BF0BB902C984F4C10BDC3533E6E9";

    /// <summary>
    /// Nonce of the client
    /// </summary>
    public const string ClientNonce = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";

    /// <summary>
    /// Nonce of the server
    /// </summary>
    public const string ServerNonce = "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F";

    /// <summary>
    /// Hash of the transcript given to the key schedule (SHA-256 of "BGPS test transcript")
    /// </summary>
    public const string TranscriptHash = "DFB7552747B53EE6C2C05E291AD27FBB7587227D473F2DC92F0EF9611DA4F0AF";

    /// <summary>
    /// PRK of HKDF-Extract, salt = both nonces
    /// </summary>
    public const string PseudoRandomKey = "AEDF65E2F9CC14959AC161941234492546351E84802DB216A28D47789468D7D6";

    /// <summary>
    /// Key of the records client → server
    /// </summary>
    public const string ClientToServerKey = "1057CE44B6A20ABD918F0F2D2CC36BDB09E623CCF65B881D7E65C7F3E0B3164D";

    /// <summary>
    /// Key of the records server → client
    /// </summary>
    public const string ServerToClientKey = "1630C7CFA2FBC3BD8C5DF1F9FD90081C0D943DA21FA1B84C2F6CFC4D0DFAE97E";

    /// <summary>
    /// IV of the records client → server
    /// </summary>
    public const string ClientToServerIv = "0DC43D4C7EF8625714EE3C7F";

    /// <summary>
    /// IV of the records server → client
    /// </summary>
    public const string ServerToClientIv = "0C39C7F3D75BC6EC1C47B8D6";

    /// <summary>
    /// Point of a signing key, uncompressed
    /// </summary>
    public const string SigningPoint = "0414353E3B70F0691A0AC08CEE071EF719B57DA85FC3D1D4CF0ACB14CC8BBC224FA60101A68DD307142CC9B8C3CF28F541F7BDA606D318003F015C0FD870635F3C";

    /// <summary>
    /// Message signed: the label of the server, a zero byte, the hash of the transcript
    /// </summary>
    public const string SignedMessage = "42475053207631206261636B2D766572796261636B20736572766572207369676E617475726500DFB7552747B53EE6C2C05E291AD27FBB7587227D473F2DC92F0EF9611DA4F0AF";

    /// <summary>
    /// Signature of the message by the signing key, IEEE P1363 (r then s)
    /// </summary>
    public const string Signature = "F5B46B3349D9BFFC339D899D971CCEDEAFD1A0380CBCD5F057C9D54A0AAF654277D58EDB5D146E8A03383A0D84A8C6AB448F153087A7BAD6AADAD2BB849D84B1";

    /// <summary>
    /// Plaintext of the first record client → server ("BGPS first record")
    /// </summary>
    public const string Record0Plaintext = "42475053206669727374207265636F7264";

    /// <summary>
    /// First record client → server, sequence number 0: its length, its ciphertext, its tag
    /// </summary>
    public const string Record0 = "0011AC08A71992800FB253FAA23A1A4E009138833289B3CBB7E6951E80DDC0D6ABD4E9";

    /// <summary>
    /// Plaintext of the second record client → server ("second")
    /// </summary>
    public const string Record1Plaintext = "7365636F6E64";

    /// <summary>
    /// Second record client → server, sequence number 1
    /// </summary>
    public const string Record1 = "00067324469BB585DE6AC04AC5D5164F287D263FB0DB9388";

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
    /// Method used to read a vector
    /// </summary>
    /// <param name="hex">[Cannot be null] Hexadecimal of the vector</param>
    /// <returns>
    /// Its bytes
    /// </returns>
    public static byte[] Bytes(string hex)
    {
        // Function's return
        return Convert.FromHexString(hex);
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
