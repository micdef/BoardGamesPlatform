/*
 *	@file	src/BoardGamesPlatform.Vault/VaultText.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Turns the bytes of a secret into its text, refusing what a secret cannot be
 */

// Using
using System.Security.Cryptography;
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Helper used by the stores to decode the value of a secret
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one rule for both systems: a secret is UTF-8 text, not empty, without any control character. A line break left by an
/// editor or by <c>echo</c> would become part of a password, and the tier would fail later with an error far from its cause.</para>
/// <para><b>Behavior:</b> decodes strictly (an invalid UTF-8 sequence is refused, never replaced), erases the bytes it was given, and names the
/// secret in its errors, never its value.</para>
/// </remarks>
internal static class VaultText
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Strict UTF-8 decoder: an invalid sequence throws instead of becoming a replacement character
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
    /// Method used to decode the bytes of a secret, then to erase them
    /// </summary>
    /// <param name="bytes">[Cannot be null] Bytes of the value; erased before the method returns, whatever happens</param>
    /// <param name="name">[Cannot be null] Name of the secret in the vault, for the messages</param>
    /// <param name="location">[Cannot be null] Folder of the vault, for the messages</param>
    /// <returns>
    /// The text of the secret
    /// </returns>
    /// <exception cref="VaultException">If the value is empty, is not UTF-8, or holds a control character (a line break included)</exception>
    public static string Decode(byte[] bytes, string name, string location)
    {
        // Decoding, then erasing the bytes in every case (the text itself cannot be erased: a .NET string is immutable)
        string text;
        try
        {
            text = _strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new VaultException($"The secret {name} of the vault {location} is not UTF-8 text");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        // Checks
        if (text.Length == 0)
            throw new VaultException($"The secret {name} of the vault {location} is empty");
        if (text.Any(char.IsControl))
            throw new VaultException($"The secret {name} of the vault {location} holds a control character (a line break left by an editor or by echo?): write it without one");

        // Function's return
        return text;
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
