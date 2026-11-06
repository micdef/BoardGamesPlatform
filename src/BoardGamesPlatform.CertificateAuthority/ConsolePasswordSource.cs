/*
 *	@file	src/BoardGamesPlatform.CertificateAuthority/ConsolePasswordSource.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Reads a password from an environment variable, or asks it on the console without showing it
 */

// Using
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.CertificateAuthority;

/// <summary>
/// Password source of the command line: an environment variable, else a hidden question on the console
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> a non-empty variable wins, so that a script can run the tool; otherwise, when a person is at the console, the password
/// is typed without being shown, and a new one is typed twice. Without a variable nor a console, no password is read.</para>
/// </remarks>
internal sealed class ConsolePasswordSource : IPasswordSource
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

    /// <summary>
    /// Method used to read a line from the console without showing it
    /// </summary>
    /// <param name="prompt">Question asked</param>
    /// <returns>
    /// The text typed, until Enter
    /// </returns>
    private static string ReadHidden(string prompt)
    {
        // Asking, then reading the keys one by one, Backspace included, without echo
        Console.Error.Write(prompt);
        StringBuilder typed = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                    typed.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar))
                typed.Append(key.KeyChar);
        }
        Console.Error.WriteLine();

        // Function's return
        return typed.ToString();
    }

    #endregion

    #region Publics

    /// <inheritdoc/>
    public string? Read(string environmentVariable, string prompt, bool isNew)
    {
        // Reading the variable first
        string? fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            // Function's return
            return fromEnvironment;
        }

        // Without a person at the console, nothing can be asked
        if (Console.IsInputRedirected)
        {
            // Function's return
            return null;
        }

        // Asking, twice for a new password
        string password = ReadHidden($"{prompt}: ");
        if (isNew && ReadHidden($"{prompt} (again): ") != password)
        {
            // Function's return
            return null;
        }

        // Function's return
        return password;
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
