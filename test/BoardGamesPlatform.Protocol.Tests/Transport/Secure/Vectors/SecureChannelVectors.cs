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
    /// Message signed by the client: its label, a zero byte, the hash of the transcript
    /// </summary>
    public const string ClientSignedMessage = "42475053207631206261636B2D766572796261636B20636C69656E74207369676E617475726500DFB7552747B53EE6C2C05E291AD27FBB7587227D473F2DC92F0EF9611DA4F0AF";

    /// <summary>
    /// Signature of the client message by the signing key, IEEE P1363
    /// </summary>
    public const string ClientSignature = "C0677305D43F997C8E1BDE758DC77890BA04A0BAB698B008DD3E99B2E54AC617289FEF8EA08753F610FFD4B9F3FD956635FF2D9256CE3A2DE800618D7504B4A8";

    /// <summary>
    /// Hash of the certificate of the server of the transcript vector (SHA-256 of "BGPS test server certificate")
    /// </summary>
    public const string TranscriptServerCertificate = "8ADB06240F640874DFF1C6F6CE7262105D08FE5FEA0E9A7CBBD520529D745925";

    /// <summary>
    /// Hash of the certificate of the client of the transcript vector (SHA-256 of "BGPS test client certificate")
    /// </summary>
    public const string TranscriptClientCertificate = "982E903E3F0103CFFDD029E7D706739B2D88DBA8AE812ADCBF153D4D0192CC93";

    /// <summary>
    /// T2 of the transcript vector: M1 = bytes 0 to 134, M2 = byte i * 7 mod 256 for i from 0 to 198, without its signature
    /// </summary>
    public const string TranscriptT2 = "0CF66E93E82B0464639DC338787CD909831E8990F910861E159126791AD40D81";

    /// <summary>
    /// T3 of the transcript vector: M1 and the whole M2
    /// </summary>
    public const string TranscriptT3 = "02BEBF3F7A15BE51D34332E30F06EDA0170EA97DA39B7F63C8DAA88C3F8A1DA4";

    /// <summary>
    /// TH of the transcript vector: M1, M2 and M3 = bytes 100 to 163
    /// </summary>
    public const string TranscriptTh = "43FDCE3939D09556EA4F960C4F8DC60063A77A4A5C2776847F4ED729A17DF707";

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
