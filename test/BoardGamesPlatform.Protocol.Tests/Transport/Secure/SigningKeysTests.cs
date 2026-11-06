/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SigningKeysTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the load, the checks and the reload of the key files of the secure channel
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="SigningKeys"/> and <see cref="SigningKeySet"/>
/// </summary>
public sealed class SigningKeysTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Password of the signing keys of the tests
    /// </summary>
    private const string Password = "signing key password";

    /// <summary>
    /// Password of the PFX of the tests
    /// </summary>
    private const string CertificatePassword = "certificate password";

    #endregion

    #region Variables

    /// <summary>
    /// Temporary folder of the test
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("bgp-secure-tests-").FullName;

    /// <summary>
    /// Logger of the keys
    /// </summary>
    private readonly CapturingLogger<SigningKeysTests> _logger = new CapturingLogger<SigningKeysTests>();

    /// <summary>
    /// Authority of the certificates of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// TLS certificate of the tier
    /// </summary>
    private readonly X509Certificate2 _certificate;

    /// <summary>
    /// Signing key of the tier
    /// </summary>
    private readonly ECDsa _ownKey = SecureChannelTestKeys.CreateSigningKey();

    /// <summary>
    /// Signing key of the peer
    /// </summary>
    private readonly ECDsa _peerKey = SecureChannelTestKeys.CreateSigningKey();

    /// <summary>
    /// Keys made by the tests
    /// </summary>
    private readonly List<ECDsa> _keys = new List<ECDsa>();

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
    /// Path of the signing key
    /// </summary>
    private string SigningKeyPath => Path.Combine(_folder, "back-signing.key");

    /// <summary>
    /// Path of the file of the public keys of the peer
    /// </summary>
    private string PeerKeysPath => Path.Combine(_folder, "veryback.pub");

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public SigningKeysTests()
    {
        // Setting
        _certificate = _certificates.Issue(PlatformRole.Back);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to make a key kept until the end of the test
    /// </summary>
    /// <returns>
    /// The key
    /// </returns>
    private ECDsa NewKey()
    {
        // Keeping it to release it
        ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        _keys.Add(key);

        // Function's return
        return key;
    }

    /// <summary>
    /// Method used to write a signing key, few iterations so that the tests stay fast
    /// </summary>
    /// <param name="key">[Cannot be null] Key to write</param>
    /// <param name="password">[Cannot be null] Its password</param>
    /// <param name="time">Time of the last write to give the file; now when null</param>
    private void WriteSigningKey(ECDsa key, string password = Password, DateTime? time = null)
    {
        // Writing, then dating the file
        File.WriteAllText(SigningKeyPath, key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        if (time is not null)
            File.SetLastWriteTimeUtc(SigningKeyPath, time.Value);
    }

    /// <summary>
    /// Method used to write the file of the public keys of the peer
    /// </summary>
    /// <param name="time">Time of the last write to give the file; now when null</param>
    /// <param name="lines">[Cannot be null] Lines of the file</param>
    private void WritePeerKeys(DateTime? time, params string[] lines)
    {
        // Writing, then dating the file
        File.WriteAllLines(PeerKeysPath, lines);
        if (time is not null)
            File.SetLastWriteTimeUtc(PeerKeysPath, time.Value);
    }

    /// <summary>
    /// Method used to write the line of a public key
    /// </summary>
    /// <param name="key">[Cannot be null] Key</param>
    /// <returns>
    /// Its Base64 SubjectPublicKeyInfo
    /// </returns>
    private static string Line(ECDsa key)
    {
        // Function's return
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Method used to load the keys of the files
    /// </summary>
    /// <param name="clock">Clock of the checks; the system when null</param>
    /// <returns>
    /// The keys
    /// </returns>
    private SigningKeys Load(TimeProvider? clock = null)
    {
        // Function's return
        return SigningKeys.FromFiles(SigningKeyPath, Password, PeerKeysPath, CertificatePassword, () => _certificate, clock ?? TimeProvider.System, _logger);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Valid files load, the comments and blank lines skipped, and the key ids are logged
    /// </summary>
    [Fact]
    public void ValidFilesLoad()
    {
        // Arranging
        WriteSigningKey(_ownKey);
        WritePeerKeys(null, "# Very Back, key of 2026", string.Empty, Line(_peerKey), "  ");

        // Acting
        using SigningKeys keys = Load();

        // Checks
        keys.Current.OwnKey.ShouldBe(SigningPublicKey.FromKey(_ownKey));
        keys.Current.PeerKeys.ShouldBe([SigningPublicKey.FromKey(_peerKey)]);
        keys.IsPinned(SigningPublicKey.FromKey(_peerKey)).ShouldBeTrue();
        keys.IsPinned(SigningPublicKey.FromKey(_ownKey)).ShouldBeFalse();
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message.Contains(keys.Current.OwnKey.KeyIdHex, StringComparison.Ordinal));
    }

    /// <summary>
    /// Each wrong file is refused at load, so that a tier never starts with keys its peer would refuse
    /// </summary>
    /// <param name="problem">What is wrong</param>
    [Theory]
    [InlineData("noPeerKey")]
    [InlineData("fivePeerKeys")]
    [InlineData("duplicate")]
    [InlineData("ownKey")]
    [InlineData("badLine")]
    [InlineData("otherCurve")]
    [InlineData("wrongPassword")]
    [InlineData("missingKey")]
    [InlineData("certificateKey")]
    public void WrongFilesAreRefused(string problem)
    {
        // Arranging the files, then the problem
        WriteSigningKey(_ownKey);
        WritePeerKeys(null, Line(_peerKey));
        switch (problem)
        {
            case "noPeerKey":
                WritePeerKeys(null, "# nothing");
                break;
            case "fivePeerKeys":
                WritePeerKeys(null, Line(NewKey()), Line(NewKey()), Line(NewKey()), Line(NewKey()), Line(NewKey()));
                break;
            case "duplicate":
                WritePeerKeys(null, Line(_peerKey), Line(_peerKey));
                break;
            case "ownKey":
                WritePeerKeys(null, Line(_peerKey), Line(_ownKey));
                break;
            case "badLine":
                WritePeerKeys(null, Line(_peerKey), "not a key");
                break;
            case "otherCurve":
                using (ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384))
                    WritePeerKeys(null, Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo()));
                break;
            case "wrongPassword":
                WriteSigningKey(_ownKey, "another password");
                break;
            case "missingKey":
                File.Delete(SigningKeyPath);
                break;
            default:
                WriteSigningKey(_certificate.GetECDsaPrivateKey()!);
                break;
        }

        // Check
        Should.Throw<InvalidOperationException>(() => Load());
    }

    /// <summary>
    /// A password of the signing key equal to the password of the PFX is refused
    /// </summary>
    [Fact]
    public void ThePasswordOfThePfxIsRefused()
    {
        // Arranging
        WriteSigningKey(_ownKey, CertificatePassword);
        WritePeerKeys(null, Line(_peerKey));

        // Check
        Should.Throw<ArgumentException>(() => SigningKeys.FromFiles(SigningKeyPath, CertificatePassword, PeerKeysPath, CertificatePassword, () => _certificate,
            TimeProvider.System, _logger));
    }

    /// <summary>
    /// A changed list is taken by the next connection, logged with the keys added and removed, and a removed key is told to the connections
    /// </summary>
    [Fact]
    public void AChangedListIsTakenAndARemovedKeyIsTold()
    {
        // Arranging
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        using SigningKeys keys = Load();
        List<SigningPublicKey> told = new List<SigningPublicKey>();
        keys.PeerKeysRemoved += (_, removed) => told.AddRange(removed.RemovedKeys);
        ECDsa newPeerKey = NewKey();

        // Acting: the key of the peer replaced
        WritePeerKeys(before.AddMinutes(1), Line(newPeerKey));
        SigningKeySet next = keys.GetForNewConnection();

        // Checks
        next.PeerKeys.ShouldBe([SigningPublicKey.FromKey(newPeerKey)]);
        next.Generation.ShouldBe(1);
        keys.IsPinned(SigningPublicKey.FromKey(_peerKey)).ShouldBeFalse();
        told.ShouldBe([SigningPublicKey.FromKey(_peerKey)]);
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(SigningPublicKey.FromKey(newPeerKey).KeyIdHex, StringComparison.Ordinal)
            && entry.Message.Contains(SigningPublicKey.FromKey(_peerKey).KeyIdHex, StringComparison.Ordinal));
    }

    /// <summary>
    /// A file copied with its old time is still seen, by its content
    /// </summary>
    [Fact]
    public void AFileWithItsOldTimeIsStillSeen()
    {
        // Arranging
        DateTime time = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: time);
        WritePeerKeys(time, Line(_peerKey));
        using SigningKeys keys = Load();
        ECDsa added = NewKey();

        // Acting: a second key, the time of the file kept
        WritePeerKeys(time, Line(_peerKey), Line(added));
        keys.ReloadIfChanged();

        // Check
        keys.Current.PeerKeys.Count.ShouldBe(2);
    }

    /// <summary>
    /// A broken file keeps the current keys, logged as an error, until the file changes once more
    /// </summary>
    [Fact]
    public void ABrokenFileKeepsTheCurrentKeys()
    {
        // Arranging
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        using SigningKeys keys = Load();
        SigningKeySet current = keys.Current;

        // Acting: the list broken, then fixed
        WritePeerKeys(before.AddMinutes(1), "broken");
        keys.ReloadIfChanged();
        SigningKeySet kept = keys.Current;
        ECDsa fixedKey = NewKey();
        WritePeerKeys(before.AddMinutes(2), Line(fixedKey));
        keys.ReloadIfChanged();

        // Checks
        kept.ShouldBeSameAs(current);
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error);
        keys.Current.PeerKeys.ShouldBe([SigningPublicKey.FromKey(fixedKey)]);
    }

    /// <summary>
    /// A new signing key is taken hot, with the same password, and logged
    /// </summary>
    [Fact]
    public void ANewSigningKeyIsTakenHot()
    {
        // Arranging
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        using SigningKeys keys = Load();
        ECDsa newKey = NewKey();

        // Acting
        WriteSigningKey(newKey, time: before.AddMinutes(1));
        SigningKeySet next = keys.GetForNewConnection();

        // Checks
        next.OwnKey.ShouldBe(SigningPublicKey.FromKey(newKey));
        _logger.Entries.ShouldContain(entry => entry.Message.Contains(next.OwnKey.KeyIdHex, StringComparison.Ordinal) && entry.Message.Contains("taken", StringComparison.Ordinal));
    }

    /// <summary>
    /// The periodic check takes a removed key without any new connection
    /// </summary>
    [Fact]
    public void ThePeriodicCheckTakesARemovedKey()
    {
        // Arranging
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        ECDsa second = NewKey();
        WritePeerKeys(before, Line(_peerKey), Line(second));
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using SigningKeys keys = Load(clock);
        List<SigningPublicKey> told = new List<SigningPublicKey>();
        keys.PeerKeysRemoved += (_, removed) => told.AddRange(removed.RemovedKeys);

        // Acting
        WritePeerKeys(before.AddMinutes(1), Line(second));
        clock.Advance(SigningKeys.CheckInterval);

        // Check
        told.ShouldBe([SigningPublicKey.FromKey(_peerKey)]);
    }

    /// <summary>
    /// A TLS certificate reloaded with the signing key as its key refuses the new connections
    /// </summary>
    [Fact]
    public void ACertificateReloadedWithTheSigningKeyRefusesNewConnections()
    {
        // Arranging: the certificate of the tier changes to one made with the signing key
        WriteSigningKey(_ownKey);
        WritePeerKeys(null, Line(_peerKey));
        X509Certificate2 current = _certificate;
        using SigningKeys keys = SigningKeys.FromFiles(SigningKeyPath, Password, PeerKeysPath, CertificatePassword, () => current, TimeProvider.System, _logger);
        CertificateRequest request = new CertificateRequest("CN=back.bgp.internal", _ownKey, HashAlgorithmName.SHA256);
        using X509Certificate2 sameKey = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // Acting
        current = sameKey;

        // Check
        Should.Throw<InvalidOperationException>(keys.GetForNewConnection);
    }

    /// <summary>
    /// A file of public keys in UTF-8 with a byte order mark is read; one in UTF-16 is refused
    /// </summary>
    [Fact]
    public void TheFileOfThePeerIsReadAsUtf8Only()
    {
        // Arranging
        WriteSigningKey(_ownKey);
        File.WriteAllText(PeerKeysPath, Line(_peerKey) + Environment.NewLine, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using (SigningKeys keys = Load())
            keys.Current.PeerKeys.ShouldBe([SigningPublicKey.FromKey(_peerKey)]);

        // Acting
        File.WriteAllText(PeerKeysPath, Line(_peerKey) + Environment.NewLine, System.Text.Encoding.Unicode);

        // Check
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("UTF-8");
    }

    /// <summary>
    /// Files that stay refused are told again every five minutes, not at every check, until they are fixed
    /// </summary>
    [Fact]
    public void FilesThatStayRefusedAreToldAgainAtTheirPace()
    {
        // Arranging: loaded keys, then a broken list
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using SigningKeys keys = Load(clock);
        WritePeerKeys(before.AddMinutes(1), "broken");

        // Acting: a first check, a second one at once, then one five minutes later
        keys.ReloadIfChanged();
        int afterFirst = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);
        keys.ReloadIfChanged();
        int afterSecond = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);
        clock.Advance(SigningKeys.RefusalRepeat);
        keys.ReloadIfChanged();
        int afterPace = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);

        // Checks
        afterFirst.ShouldBe(1);
        afterSecond.ShouldBe(1);
        afterPace.ShouldBe(2);
        keys.IsPinned(SigningPublicKey.FromKey(_peerKey)).ShouldBeTrue();
    }

    /// <summary>
    /// Files that could not be read for a while, then readable again unchanged, are fine again: the refusal is not told once more
    /// </summary>
    [Fact]
    public void FilesReadableAgainUnchangedAreNoLongerRefused()
    {
        // Arranging: loaded keys
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using SigningKeys keys = Load(clock);

        // Acting: the list missing for a check, back with the same content and time, then a check after the pace of the refusals
        File.Delete(PeerKeysPath);
        keys.ReloadIfChanged();
        int afterMissing = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);
        WritePeerKeys(before, Line(_peerKey));
        keys.ReloadIfChanged();
        clock.Advance(SigningKeys.RefusalRepeat);
        keys.ReloadIfChanged();
        int afterBack = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);

        // Checks
        afterMissing.ShouldBe(1);
        afterBack.ShouldBe(1);
        keys.IsPinned(SigningPublicKey.FromKey(_peerKey)).ShouldBeTrue();
    }

    /// <summary>
    /// Refused files that could not be read for a while, then readable again unchanged, are still refused, and told again at their pace
    /// </summary>
    [Fact]
    public void RefusedFilesStayRefusedAfterAPassingReadFailure()
    {
        // Arranging: loaded keys, then a broken list
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        WritePeerKeys(before, Line(_peerKey));
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using SigningKeys keys = Load(clock);
        DateTime broken = before.AddMinutes(1);
        WritePeerKeys(broken, "broken");
        keys.ReloadIfChanged();

        // Acting: the broken list missing for a check, back unchanged, then a check after the pace of the refusals
        File.Delete(PeerKeysPath);
        keys.ReloadIfChanged();
        WritePeerKeys(broken, "broken");
        keys.ReloadIfChanged();
        int beforePace = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);
        clock.Advance(SigningKeys.RefusalRepeat);
        keys.ReloadIfChanged();
        int afterPace = _logger.Entries.Count(entry => entry.Level == LogLevel.Error);

        // Checks: told once, then again at the pace, the old keys kept
        beforePace.ShouldBe(1);
        afterPace.ShouldBe(2);
        keys.IsPinned(SigningPublicKey.FromKey(_peerKey)).ShouldBeTrue();
    }

    /// <summary>
    /// A listener of the removed keys that fails is logged, and never keeps the next listeners from being told nor stops the reload
    /// </summary>
    [Fact]
    public void AFailingListenerKeepsTheOthersTold()
    {
        // Arranging two listeners, the first one failing
        DateTime before = DateTime.UtcNow.AddMinutes(-10);
        WriteSigningKey(_ownKey, time: before);
        ECDsa second = NewKey();
        WritePeerKeys(before, Line(_peerKey), Line(second));
        using SigningKeys keys = Load();
        bool isSecondTold = false;
        keys.PeerKeysRemoved += (_, _) => throw new InvalidOperationException("A failing listener of the test");
        keys.PeerKeysRemoved += (_, _) => isSecondTold = true;

        // Acting
        WritePeerKeys(before.AddMinutes(1), Line(second));
        keys.ReloadIfChanged();

        // Checks
        isSecondTold.ShouldBeTrue();
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be told", StringComparison.Ordinal));
    }

    /// <summary>
    /// A set refuses a public key as its signing key
    /// </summary>
    [Fact]
    public void ASetNeedsAPrivateSigningKey()
    {
        // Arranging
        ECDsa publicOnly = ECDsa.Create();
        _keys.Add(publicOnly);
        publicOnly.ImportSubjectPublicKeyInfo(_ownKey.ExportSubjectPublicKeyInfo(), out _);

        // Check
        Should.Throw<InvalidOperationException>(() => SigningKeySet.Create(publicOnly, [SigningPublicKey.FromKey(_peerKey)], 0));
    }

    /// <summary>
    /// Method used to release the files and the keys
    /// </summary>
    public void Dispose()
    {
        // Releasing everything the test made
        _certificates.Dispose();
        _ownKey.Dispose();
        _peerKey.Dispose();
        foreach (ECDsa key in _keys)
            key.Dispose();
        Directory.Delete(_folder, recursive: true);
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
