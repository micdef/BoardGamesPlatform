/*
 *	@file	src/BoardGamesPlatform.CertificateAuthority/IPasswordSource.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Where the certificate authority tool reads the passwords that protect its keys
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.CertificateAuthority;

/// <summary>
/// Source of the passwords of the certificate authority tool
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the tool never takes a password on its command line, where the process list would show it; the tests give theirs
/// without a console.</para>
/// </remarks>
internal interface IPasswordSource
{
    #region Methods

    /// <summary>
    /// Method used to read a password
    /// </summary>
    /// <param name="environmentVariable">[Cannot be null or blank] Environment variable read first</param>
    /// <param name="prompt">[Cannot be null or blank] Question asked when the variable is not set</param>
    /// <param name="isNew">True when the password protects a new key: it is then asked twice when typed</param>
    /// <returns>
    /// The password<br/>
    /// Null ==> No password could be read (no variable and no console), or the two typed passwords differ
    /// </returns>
    public string? Read(string environmentVariable, string prompt, bool isNew);

    #endregion
}
