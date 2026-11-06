/*
 *	@file	src/BoardGamesPlatform.CertificateAuthority/AuthorityCommands.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Commands of the offline certificate authority tool: init, request, sign, install and list
 */

// Using
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BoardGamesPlatform.Protocol.Transport.Tls;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.CertificateAuthority;

/// <summary>
/// Commands of the certificate authority tool
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the platform trusts its own authority only. Its root key stays on one machine, used by hand when a tier needs a
/// certificate: the tool opens no port, so an authority that is not running cannot be attacked.</para>
/// <para><b>Behavior:</b> the steps, in order:</para>
/// <list type="number">
/// <item><description><c>init</c>, on the machine of the authority: creates the root (<c>platform-ca.pfx</c>, protected by its password, and
/// <c>platform-ca.crt</c>, the public certificate every tier trusts).</description></item>
/// <item><description><c>request</c>, on the machine of the tier: creates its key (<c>&lt;role&gt;.key</c>, encrypted) and its request
/// (<c>&lt;role&gt;.csr</c>). The key never leaves the machine.</description></item>
/// <item><description><c>sign</c>, on the machine of the authority: signs the request for its role (<c>&lt;role&gt;.crt</c>) and records it in
/// <c>issued.csv</c>.</description></item>
/// <item><description><c>install</c>, on the machine of the tier: joins the certificate and the key into the PFX the tier loads, after checking
/// that the certificate comes from the authority and matches the key.</description></item>
/// <item><description><c>list</c>: shows the certificates issued, to renew them in time or to refuse one.</description></item>
/// </list>
/// <para>No command ever overwrites a file. The passwords are read from <see cref="AuthorityPasswordVariable"/> and
/// <see cref="CertificatePasswordVariable"/>, else asked on the console, never on the command line; a new password has at least
/// <see cref="MinPasswordLength"/> characters. On Linux, the files that hold a private key are readable by their owner only.</para>
/// </remarks>
internal sealed class AuthorityCommands
{
    #region Members

    #region Constants

    /// <summary>
    /// Environment variable holding the password of the root key
    /// </summary>
    public const string AuthorityPasswordVariable = "BOARDGAMESPLATFORM_CA_PASSWORD";

    /// <summary>
    /// Environment variable holding the password of the key and of the PFX of a tier
    /// </summary>
    public const string CertificatePasswordVariable = "BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD";

    /// <summary>
    /// Shortest password accepted for a new key
    /// </summary>
    public const int MinPasswordLength = 12;

    /// <summary>
    /// File of the root, with its private key
    /// </summary>
    public const string AuthorityKeyFile = "platform-ca.pfx";

    /// <summary>
    /// File of the public certificate of the root, given to every tier
    /// </summary>
    public const string AuthorityCertificateFile = "platform-ca.crt";

    /// <summary>
    /// File that records the certificates issued
    /// </summary>
    public const string IssuedFile = "issued.csv";

    /// <summary>
    /// First line of the file of the certificates issued
    /// </summary>
    public const string IssuedHeader = "issuedUtc,serialNumber,thumbprintSha256,role,dnsName,notBeforeUtc,notAfterUtc";

    /// <summary>
    /// Validity of a certificate of a tier when none is given, in days
    /// </summary>
    public const int DefaultValidityDays = 365;

    /// <summary>
    /// Validity of the root when none is given, in years
    /// </summary>
    public const int DefaultRootValidityYears = 10;

    /// <summary>
    /// Iterations of the derivation of the password of an encrypted key (PBKDF2 with SHA-256)
    /// </summary>
    private const int KeyDerivationIterations = 600_000;

    /// <summary>
    /// Number of iterations of the key derivation of a PFX (the root and the PFX of a tier): the highest one .NET loads with its default
    /// limits (<see cref="Pkcs12LoaderLimits"/>, 300 000 at most), far above the 2 000 of the default export
    /// </summary>
    private const int PfxKeyDerivationIterations = 300_000;

    /// <summary>
    /// Margin given to the clocks: a certificate is valid from this long before it is signed, so that a tier whose clock is a little late does
    /// not refuse it
    /// </summary>
    private static readonly TimeSpan _clockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Text of the help
    /// </summary>
    private const string Usage = """
        Offline certificate authority of BoardGamesPlatform

        Usage:
          init    --dir <folder> --name <common name> [--years <1-20>]
          request --dir <folder> --role <gateway|back|veryback> --suffix <suffix>
          sign    --dir <folder> --request <file.csr> --role <gateway|back|veryback> --suffix <suffix> [--days <1-397>] --out <file.crt>
          install --key <file.key> --certificate <file.crt> --authority <platform-ca.crt> --out <file.pfx>
          list    --dir <folder>

        Passwords: BOARDGAMESPLATFORM_CA_PASSWORD (root key) and BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD (key and PFX of a tier),
        else asked on the console. No file is ever overwritten.
        """;

    #endregion

    #region Variables

    /// <summary>
    /// Where the passwords are read
    /// </summary>
    private readonly IPasswordSource _passwords;

    /// <summary>
    /// Where the results are written
    /// </summary>
    private readonly TextWriter _output;

    /// <summary>
    /// Where the errors are written
    /// </summary>
    private readonly TextWriter _error;

    /// <summary>
    /// Clock of the validity (the system clock outside the tests)
    /// </summary>
    private readonly TimeProvider _timeProvider;

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
    /// <param name="passwords">[Cannot be null] Where the passwords are read</param>
    /// <param name="output">[Cannot be null] Where the results are written</param>
    /// <param name="error">[Cannot be null] Where the errors are written</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the validity</param>
    public AuthorityCommands(IPasswordSource passwords, TextWriter output, TextWriter error, TimeProvider timeProvider)
    {
        // Setting
        _passwords = passwords;
        _output = output;
        _error = error;
        _timeProvider = timeProvider;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to read the options of a command, each one given once as "--name value"
    /// </summary>
    /// <param name="args">Arguments after the name of the command</param>
    /// <param name="allowed">Names of the options the command takes</param>
    /// <returns>
    /// The options by name
    /// </returns>
    /// <exception cref="UsageException">If an option is unknown, repeated or has no value</exception>
    private static Dictionary<string, string> ReadOptions(string[] args, params string[] allowed)
    {
        // Reading the pairs
        Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || !allowed.Contains(name[2..]))
                throw new UsageException($"Unknown option: {name}");
            if (index + 1 >= args.Length)
                throw new UsageException($"The option {name} needs a value");
            if (!options.TryAdd(name[2..], args[index + 1]))
                throw new UsageException($"The option {name} is given twice");
        }

        // Function's return
        return options;
    }

    /// <summary>
    /// Method used to read a mandatory option
    /// </summary>
    /// <param name="options">[Cannot be null] Options of the command</param>
    /// <param name="name">[Cannot be null] Name of the option</param>
    /// <returns>
    /// The value of the option
    /// </returns>
    /// <exception cref="UsageException">If the option is missing or blank</exception>
    private static string Require(Dictionary<string, string> options, string name)
    {
        // Function's return
        return options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new UsageException($"The option --{name} is mandatory");
    }

    /// <summary>
    /// Method used to read an optional whole number within bounds
    /// </summary>
    /// <param name="options">[Cannot be null] Options of the command</param>
    /// <param name="name">[Cannot be null] Name of the option</param>
    /// <param name="defaultValue">Value when the option is missing</param>
    /// <param name="min">Smallest value accepted</param>
    /// <param name="max">Largest value accepted</param>
    /// <returns>
    /// The value
    /// </returns>
    /// <exception cref="UsageException">If the value is not a whole number within the bounds</exception>
    private static int ReadNumber(Dictionary<string, string> options, string name, int defaultValue, int min, int max)
    {
        // Using the default when the option is missing
        if (!options.TryGetValue(name, out string? text))
        {
            // Function's return
            return defaultValue;
        }

        // Function's return
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max
            ? value
            : throw new UsageException($"The option --{name} must be a whole number from {min} to {max}");
    }

    /// <summary>
    /// Method used to read the role and the suffix of a command
    /// </summary>
    /// <param name="options">[Cannot be null] Options of the command</param>
    /// <param name="suffix">The suffix of the deployment</param>
    /// <returns>
    /// The role
    /// </returns>
    /// <exception cref="UsageException">If the role is unknown or the suffix invalid</exception>
    private static PlatformRole ReadIdentity(Dictionary<string, string> options, out string suffix)
    {
        // Reading and checking both
        if (!PlatformIdentity.TryParseRole(Require(options, "role"), out PlatformRole role))
            throw new UsageException("The option --role must be gateway, back or veryback");
        suffix = Require(options, "suffix");
        if (!PlatformIdentity.IsValidSuffix(suffix))
            throw new UsageException("The option --suffix must be DNS labels of lower-case letters, digits and inner hyphens, for example bgp.internal");

        // Function's return
        return role;
    }

    /// <summary>
    /// Method used to read a password, refusing a missing one, and a short one for a new key
    /// </summary>
    /// <param name="variable">[Cannot be null] Environment variable read first</param>
    /// <param name="prompt">[Cannot be null] Question asked on the console</param>
    /// <param name="isNew">True when the password protects a new key</param>
    /// <returns>
    /// The password
    /// </returns>
    /// <exception cref="UsageException">If no password is given, the two typed ones differ, or a new one is too short</exception>
    private string ReadPassword(string variable, string prompt, bool isNew)
    {
        // Reading, then checking
        string? password = _passwords.Read(variable, prompt, isNew);
        if (string.IsNullOrEmpty(password))
            throw new UsageException($"No password: set {variable}, or type it at the console (the two typed passwords must match)");
        if (isNew && password.Length < MinPasswordLength)
            throw new UsageException($"A new password needs at least {MinPasswordLength} characters");

        // Function's return
        return password;
    }

    /// <summary>
    /// Method used to write a new file, never overwriting one; a file that holds a private key is readable by its owner only on Linux
    /// </summary>
    /// <param name="path">[Cannot be null] Path of the file</param>
    /// <param name="content">[Cannot be null] Content of the file</param>
    /// <param name="isPrivate">True when the file holds a private key</param>
    /// <exception cref="IOException">If the file already exists, or cannot be written</exception>
    private static void WriteNew(string path, byte[] content, bool isPrivate)
    {
        // Creating the file, which fails when it exists, with its permissions set before any byte is written
        FileStreamOptions options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (isPrivate && !OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using FileStream file = new FileStream(path, options);
        file.Write(content);
    }

    /// <summary>
    /// Method used to build the encryption of a PFX: AES-256 and SHA-256, with <see cref="PfxKeyDerivationIterations"/> iterations
    /// </summary>
    /// <returns>
    /// The parameters of the encryption
    /// </returns>
    private static PbeParameters CreatePfxEncryption()
    {
        // Function's return
        return new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, PfxKeyDerivationIterations);
    }

    /// <summary>
    /// Method used to cut a time to the second, the precision of the validity of a certificate
    /// </summary>
    /// <param name="time">Time to cut</param>
    /// <returns>
    /// The time, without its fraction of a second
    /// </returns>
    private static DateTimeOffset TruncateToSecond(DateTimeOffset time)
    {
        // Function's return
        return new DateTimeOffset(time.Ticks - (time.Ticks % TimeSpan.TicksPerSecond), time.Offset);
    }

    /// <summary>
    /// Method used to load a public certificate, naming the file when it cannot be read
    /// </summary>
    /// <param name="path">[Cannot be null] File of the certificate (PEM or DER)</param>
    /// <returns>
    /// The certificate, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="CryptographicException">If the file is not a certificate</exception>
    private static X509Certificate2 LoadCertificate(string path)
    {
        try
        {
            // Function's return
            return X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException($"The file {path} is not a certificate: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Method used to refuse a command whose output files already exist, before anything is created
    /// </summary>
    /// <param name="paths">[Cannot be null] Files the command will write</param>
    /// <exception cref="UsageException">If one of them exists</exception>
    private static void RefuseExisting(params string[] paths)
    {
        // Checking every file
        foreach (string path in paths)
        {
            if (File.Exists(path))
                throw new UsageException($"The file {path} already exists: no file is ever overwritten");
        }
    }

    /// <summary>
    /// Method used to load the root with its private key
    /// </summary>
    /// <param name="directory">[Cannot be null] Folder of the authority</param>
    /// <returns>
    /// The root, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="UsageException">If the folder holds no authority</exception>
    /// <exception cref="CryptographicException">If the password is wrong or the file damaged</exception>
    private X509Certificate2 LoadAuthority(string directory)
    {
        // Loading the PFX of the root, its key kept in memory only
        string path = Path.Combine(directory, AuthorityKeyFile);
        if (!File.Exists(path))
            throw new UsageException($"No authority in {directory}: run init first");
        string password = ReadPassword(AuthorityPasswordVariable, "Password of the root key", isNew: false);

        // Function's return
        return X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
    }

    /// <summary>
    /// Method used to create the root of the authority
    /// </summary>
    /// <param name="args">[Cannot be null] Options of the command</param>
    private void Init(string[] args)
    {
        // Reading the options, and refusing an existing authority
        Dictionary<string, string> options = ReadOptions(args, "dir", "name", "years");
        string directory = Require(options, "dir");
        string name = Require(options, "name");
        int years = ReadNumber(options, "years", DefaultRootValidityYears, 1, CertificateIssuer.MaxRootValidityYears);
        string keyPath = Path.Combine(directory, AuthorityKeyFile);
        string certificatePath = Path.Combine(directory, AuthorityCertificateFile);
        RefuseExisting(keyPath, certificatePath);
        string password = ReadPassword(AuthorityPasswordVariable, "New password of the root key", isNew: true);

        // Creating the root, valid from a little before now (see _clockSkew), then writing its key and its public certificate
        DateTimeOffset start = TruncateToSecond(_timeProvider.GetUtcNow() - _clockSkew);
        using X509Certificate2 root = CertificateIssuer.CreateRoot(name, start, start.AddYears(years));
        Directory.CreateDirectory(directory);
        WriteNew(keyPath, root.ExportPkcs12(CreatePfxEncryption(), password), isPrivate: true);
        WriteNew(certificatePath, Encoding.ASCII.GetBytes(root.ExportCertificatePem()), isPrivate: false);
        _output.WriteLine($"Authority created: {root.Subject}, valid until {root.NotAfter.ToUniversalTime():yyyy-MM-dd} (UTC)");
        _output.WriteLine($"Key (keep it on this machine only): {keyPath}");
        _output.WriteLine($"Certificate to trust on every tier: {certificatePath}");
    }

    /// <summary>
    /// Method used to create the key and the request of a tier
    /// </summary>
    /// <param name="args">[Cannot be null] Options of the command</param>
    private void Request(string[] args)
    {
        // Reading the options, and refusing existing files
        Dictionary<string, string> options = ReadOptions(args, "dir", "role", "suffix");
        string directory = Require(options, "dir");
        PlatformRole role = ReadIdentity(options, out string suffix);
        string roleName = PlatformIdentity.GetRoleName(role);
        string keyPath = Path.Combine(directory, $"{roleName}.key");
        string requestPath = Path.Combine(directory, $"{roleName}.csr");
        RefuseExisting(keyPath, requestPath);
        string password = ReadPassword(CertificatePasswordVariable, "New password of the key of the tier", isNew: true);

        // Creating the key and the request, the key being written encrypted
        using ECDsa key = CertificateIssuer.CreateRequest(role, suffix, out string requestPem);
        PbeParameters encryption = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, KeyDerivationIterations);
        Directory.CreateDirectory(directory);
        WriteNew(keyPath, Encoding.ASCII.GetBytes(key.ExportEncryptedPkcs8PrivateKeyPem(password, encryption)), isPrivate: true);
        WriteNew(requestPath, Encoding.ASCII.GetBytes(requestPem), isPrivate: false);
        _output.WriteLine($"Request of {PlatformIdentity.GetDnsName(role, suffix)} created");
        _output.WriteLine($"Key (never copy it elsewhere): {keyPath}");
        _output.WriteLine($"Request to sign by the authority: {requestPath}");
    }

    /// <summary>
    /// Method used to sign the request of a tier
    /// </summary>
    /// <param name="args">[Cannot be null] Options of the command</param>
    private void Sign(string[] args)
    {
        // Reading the options, and refusing an existing certificate
        Dictionary<string, string> options = ReadOptions(args, "dir", "request", "role", "suffix", "days", "out");
        string directory = Require(options, "dir");
        string requestPath = Require(options, "request");
        PlatformRole role = ReadIdentity(options, out string suffix);
        int days = ReadNumber(options, "days", DefaultValidityDays, 1, CertificateIssuer.MaxValidityDays);
        string outputPath = Require(options, "out");
        RefuseExisting(outputPath);
        if (!File.Exists(requestPath))
            throw new UsageException($"The request {requestPath} does not exist");

        // Signing within the validity of the root, from a little before now (see _clockSkew), to the second as a certificate holds it
        using X509Certificate2 authority = LoadAuthority(directory);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset rootStart = new DateTimeOffset(authority.NotBefore.ToUniversalTime(), TimeSpan.Zero);
        DateTimeOffset rootEnd = new DateTimeOffset(authority.NotAfter.ToUniversalTime(), TimeSpan.Zero);
        DateTimeOffset notBefore = TruncateToSecond(now - _clockSkew);
        if (notBefore < rootStart)
            notBefore = rootStart;
        DateTimeOffset notAfter = TruncateToSecond(notBefore.AddDays(days));
        if (notAfter > rootEnd)
            notAfter = rootEnd;
        using X509Certificate2 certificate = CertificateIssuer.Sign(authority, File.ReadAllText(requestPath), role, suffix, notBefore, notAfter);

        // Recording the certificate first, then writing it: a line of the register without its file is a smaller harm than a certificate the
        // register does not know (the register is where a certificate to refuse is found)
        string thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        string issuedPath = Path.Combine(directory, IssuedFile);
        if (!File.Exists(issuedPath))
            File.AppendAllText(issuedPath, IssuedHeader + Environment.NewLine);
        File.AppendAllText(issuedPath, string.Join(',', now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), certificate.SerialNumber, thumbprint,
            PlatformIdentity.GetRoleName(role), PlatformIdentity.GetDnsName(role, suffix), certificate.NotBefore.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            certificate.NotAfter.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)) + Environment.NewLine);
        WriteNew(outputPath, Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()), isPrivate: false);
        _output.WriteLine($"Certificate of {PlatformIdentity.GetDnsName(role, suffix)} signed, valid until {certificate.NotAfter.ToUniversalTime():yyyy-MM-dd} (UTC)");
        _output.WriteLine($"Thumbprint (SHA-256): {thumbprint}");
        _output.WriteLine($"Certificate to send back to the tier: {outputPath}");
    }

    /// <summary>
    /// Method used to join the certificate and the key of a tier into its PFX
    /// </summary>
    /// <param name="args">[Cannot be null] Options of the command</param>
    private void Install(string[] args)
    {
        // Reading the options, and refusing an existing PFX
        Dictionary<string, string> options = ReadOptions(args, "key", "certificate", "authority", "out");
        string keyPath = Require(options, "key");
        string certificatePath = Require(options, "certificate");
        string authorityPath = Require(options, "authority");
        string outputPath = Require(options, "out");
        RefuseExisting(outputPath);
        foreach (string input in new string[] { keyPath, certificatePath, authorityPath })
        {
            if (!File.Exists(input))
                throw new UsageException($"The file {input} does not exist");
        }

        // Checking that the certificate comes from the authority, which alone is trusted
        using X509Certificate2 certificate = LoadCertificate(certificatePath);
        using X509Certificate2 authority = LoadCertificate(authorityPath);
        using X509Chain chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = _timeProvider.GetUtcNow().UtcDateTime;
        if (!chain.Build(certificate))
            throw new CryptographicException($"The certificate does not come from this authority, or is not valid now: {string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()))}");

        // Joining it with its key, which fails when the key is not the one of the certificate, then writing the PFX
        string password = ReadPassword(CertificatePasswordVariable, "Password of the key of the tier", isNew: false);
        using ECDsa key = ECDsa.Create();
        key.ImportFromEncryptedPem(File.ReadAllText(keyPath), password);
        using X509Certificate2 joined = certificate.CopyWithPrivateKey(key);
        WriteNew(outputPath, joined.ExportPkcs12(CreatePfxEncryption(), password), isPrivate: true);
        _output.WriteLine($"PFX of {certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false)} written: {outputPath}");
        _output.WriteLine($"Delete the key file now that the PFX holds it: {keyPath}");
    }

    /// <summary>
    /// Method used to show the certificates issued
    /// </summary>
    /// <param name="args">[Cannot be null] Options of the command</param>
    private void List(string[] args)
    {
        // Reading the record of the authority
        Dictionary<string, string> options = ReadOptions(args, "dir");
        string issuedPath = Path.Combine(Require(options, "dir"), IssuedFile);
        if (!File.Exists(issuedPath))
        {
            _output.WriteLine("No certificate issued yet");
            return;
        }
        foreach (string line in File.ReadAllLines(issuedPath))
            _output.WriteLine(line);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to run one command
    /// </summary>
    /// <param name="args">[Cannot be null] Arguments of the tool: the command, then its options</param>
    /// <returns>
    /// 0 ==> The command succeeded<br/>
    /// 1 ==> The command failed (a file, a password, a key or a certificate is wrong); nothing was written over<br/>
    /// 2 ==> The command line is wrong; the help is written
    /// </returns>
    public int Run(string[] args)
    {
        try
        {
            // Running the command named first
            string[] options = args.Skip(1).ToArray();
            switch (args.FirstOrDefault())
            {
                case "init":
                    Init(options);
                    break;
                case "request":
                    Request(options);
                    break;
                case "sign":
                    Sign(options);
                    break;
                case "install":
                    Install(options);
                    break;
                case "list":
                    List(options);
                    break;
                default:
                    throw new UsageException(args.Length == 0 ? "No command" : $"Unknown command: {args[0]}");
            }
        }
        catch (UsageException exception)
        {
            _error.WriteLine(exception.Message);
            _error.WriteLine();
            _error.WriteLine(Usage);

            // Function's return
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            _error.WriteLine($"Failed: {exception.Message}");

            // Function's return
            return 1;
        }
        catch (Exception exception)
        {
            // Any other failure is a failure of the command too: the exit code tells it, never a crash of the tool
            _error.WriteLine($"Failed (unexpected {exception.GetType().Name}): {exception.Message}");

            // Function's return
            return 1;
        }

        // Function's return
        return 0;
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

    /// <summary>
    /// Error of the command line, answered with the help
    /// </summary>
    /// <param name="message">What is wrong</param>
    private sealed class UsageException(string message) : Exception(message);
}
