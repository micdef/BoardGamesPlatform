/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/JsonFuzzKit.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Helpers shared by the fuzz tests of the JSON readers: random inputs, mutations of a valid message and checks of the refusals
 */

// Using
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardGamesPlatform.Protocol.Errors;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Random inputs, mutations and checks shared by the fuzz tests of the envelope reader and of the control message reader
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> both readers promise the same things (only <see cref="InvalidDataException"/> for malformed input, no text of the
/// peer in the error messages), so they are attacked with the same inputs and judged by the same checks.</para>
/// <para><b>Behavior:</b> every random value comes from a <see cref="Random"/> given by the caller, built with a fixed seed: a failure
/// message prints its context (seed, mutation) and replays identically.</para>
/// </remarks>
internal static class JsonFuzzKit
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of seeds, hence of random cases, run by each random test; the seeds are 1 to this value
    /// </summary>
    internal const int SeedCount = 200;

    /// <summary>
    /// Longest random byte sequence given to a reader, in bytes
    /// </summary>
    internal const int MaxRandomLength = 2048;

    /// <summary>
    /// Characters over which the mutated bytes are drawn: the JSON structure and its literals, so that mutations reach the parser's states
    /// </summary>
    private const string JsonAlphabet = "{}[]\",:\\0123456789-+.eEtrufalsn \n";

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
    /// Method used to list the paths of the properties of a JSON object, at its first two levels
    /// </summary>
    /// <param name="root">[Cannot be null] Object to walk</param>
    /// <returns>
    /// The paths, each one being the keys from the root to the property
    /// </returns>
    private static List<string[]> ListPaths(JsonObject root)
    {
        // Walking the properties
        List<string[]> paths = new List<string[]>();
        foreach (KeyValuePair<string, JsonNode?> property in root)
        {
            paths.Add([property.Key]);
            if (property.Value is JsonObject child)
            {
                foreach (KeyValuePair<string, JsonNode?> childProperty in child)
                    paths.Add([property.Key, childProperty.Key]);
            }
        }

        // Function's return
        return paths;
    }

    /// <summary>
    /// Method used to parse a valid message again and reach the object holding the property of a path
    /// </summary>
    /// <param name="valid">[Cannot be null] Valid message</param>
    /// <param name="path">[Cannot be null] Keys from the root to the property</param>
    /// <returns>
    /// The root, and the object that holds the property
    /// </returns>
    private static (JsonObject Root, JsonObject Parent) Reach(byte[] valid, string[] path)
    {
        // Walking down to the parent
        JsonObject root = JsonNode.Parse(valid)!.AsObject();
        JsonObject parent = root;
        for (int index = 0; index < path.Length - 1; index++)
            parent = parent[path[index]]!.AsObject();

        // Function's return
        return (root, parent);
    }

    /// <summary>
    /// Method used to write a JSON node as UTF-8
    /// </summary>
    /// <param name="node">[Cannot be null] Node to write</param>
    /// <returns>
    /// The compact JSON
    /// </returns>
    private static byte[] ToBytes(JsonNode node)
    {
        // Function's return
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    /// <summary>
    /// Method used to get the kind of a JSON node, the null node being a null
    /// </summary>
    /// <param name="node">Node to look at, null for the JSON value null</param>
    /// <returns>
    /// The kind of the node
    /// </returns>
    private static JsonValueKind KindOf(JsonNode? node)
    {
        // Function's return
        return node is null ? JsonValueKind.Null : node.GetValueKind();
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to build random bytes
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// Between 0 and <see cref="MaxRandomLength"/> random bytes
    /// </returns>
    internal static byte[] NextRandomBytes(Random random)
    {
        // Function's return
        byte[] bytes = new byte[random.Next(0, MaxRandomLength + 1)];
        random.NextBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Method used to build a valid message with a few bytes overwritten and, sometimes, cut short
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the new bytes are mostly characters of JSON (see <see cref="JsonAlphabet"/>), sometimes any byte.</para>
    /// </remarks>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <param name="valid">[Cannot be null] Valid message, left untouched</param>
    /// <returns>
    /// The mutated copy
    /// </returns>
    internal static byte[] NextMutation(Random random, byte[] valid)
    {
        // Overwriting bytes
        byte[] bytes = (byte[])valid.Clone();
        int changes = random.Next(1, 6);
        for (int change = 0; change < changes; change++)
        {
            bytes[random.Next(0, bytes.Length)] = random.Next(0, 4) == 0
                ? (byte)random.Next(0, 256)
                : (byte)JsonAlphabet[random.Next(0, JsonAlphabet.Length)];
        }

        // Function's return
        return random.Next(0, 4) == 0 ? bytes[..random.Next(0, bytes.Length + 1)] : bytes;
    }

    /// <summary>
    /// Method used to build a random marker, a text that no message of the library contains and no valid name or code can be
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> it always holds a <c>#</c> and an underscore, so it is neither a well-formed error code nor a valid operation
    /// name; depending on the draw it also holds non-ASCII characters, or a control character, a quote and a backslash.</para>
    /// </remarks>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// The marker
    /// </returns>
    internal static string NextMarker(Random random)
    {
        // Building the marker
        string marker = $"Mk_#{random.Next():x8}";

        // Function's return
        return random.Next(0, 3) switch
        {
            0 => marker,
            1 => marker + "é☃😀",
            _ => marker + "\u0007\n\"\\"
        };
    }

    /// <summary>
    /// Method used to quote a text as a JSON string
    /// </summary>
    /// <param name="text">[Cannot be null] Text to quote</param>
    /// <returns>
    /// The JSON string, quotes included
    /// </returns>
    internal static string Quote(string text)
    {
        // Function's return
        return JsonSerializer.Serialize(text);
    }

    /// <summary>
    /// Method used to build a random valid operation name
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// 1 to 64 characters among A-Z, a-z, 0-9, '.', '_' and '-'
    /// </returns>
    internal static string NextOperationName(Random random)
    {
        // Building the name, the shortest and the longest lengths coming up often
        const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._-";
        int choice = random.Next(0, 4);
        int length = choice == 0 ? 1 : choice == 1 ? 64 : random.Next(1, 65);
        StringBuilder name = new StringBuilder(length);
        for (int index = 0; index < length; index++)
            name.Append(Characters[random.Next(0, Characters.Length)]);

        // Function's return
        return name.ToString();
    }

    /// <summary>
    /// Method used to build a random well-formed error code
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// One to four segments separated by dots, each one starting with a lowercase letter, at most <see cref="ErrorCodes.MaxCodeLength"/> characters
    /// </returns>
    internal static string NextErrorCode(Random random)
    {
        // Building the segments
        const string Letters = "abcdefghijklmnopqrstuvwxyz";
        const string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        int segments = random.Next(1, 5);
        StringBuilder code = new StringBuilder();
        for (int segment = 0; segment < segments; segment++)
        {
            if (segment > 0)
                code.Append('.');
            code.Append(Letters[random.Next(0, Letters.Length)]);
            int length = random.Next(0, 12);
            for (int index = 0; index < length; index++)
                code.Append(Characters[random.Next(0, Characters.Length)]);
        }

        // Function's return
        return code.ToString();
    }

    /// <summary>
    /// Method used to build a random <see cref="int"/>, the edges coming up often
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// The number
    /// </returns>
    internal static int NextInt(Random random)
    {
        // Function's return
        return random.Next(0, 6) switch
        {
            0 => 0,
            1 => int.MaxValue,
            2 => int.MinValue,
            _ => random.Next(int.MinValue, int.MaxValue)
        };
    }

    /// <summary>
    /// Method used to build a random <see cref="ulong"/>, the edges coming up often
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// The number
    /// </returns>
    internal static ulong NextUlong(Random random)
    {
        // Function's return
        int choice = random.Next(0, 5);
        if (choice == 0)
            return 0;
        if (choice == 1)
            return ulong.MaxValue;
        return (ulong)random.NextInt64(long.MinValue, long.MaxValue);
    }

    /// <summary>
    /// Method used to build the prefixes of a message, one for each offset, the whole message excluded
    /// </summary>
    /// <param name="valid">[Cannot be null] Valid message</param>
    /// <returns>
    /// The prefixes, from 0 bytes to the length of the message minus one
    /// </returns>
    internal static IEnumerable<byte[]> Truncations(byte[] valid)
    {
        // Cutting at every offset
        for (int length = 0; length < valid.Length; length++)
            yield return valid[..length];
    }

    /// <summary>
    /// Method used to build the messages with one bit flipped, at every offset
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <param name="valid">[Cannot be null] Valid message</param>
    /// <returns>
    /// The mutated messages, with the offset and the bit flipped
    /// </returns>
    internal static IEnumerable<(string Name, byte[] Bytes)> BitFlips(Random random, byte[] valid)
    {
        // Flipping one random bit at every offset
        for (int offset = 0; offset < valid.Length; offset++)
        {
            int bit = random.Next(0, 8);
            byte[] bytes = (byte[])valid.Clone();
            bytes[offset] ^= (byte)(1 << bit);
            yield return ($"flip bit {bit} of byte {offset}", bytes);
        }
    }

    /// <summary>
    /// Method used to build the messages whose structure is broken in one place, each of which a strict reader must refuse
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> for every property, at the first two levels of the message: the property deleted, renamed, written again in
    /// another case, written twice, and given a value of another kind (number, string, object, array, boolean, null).</para>
    /// </remarks>
    /// <param name="valid">[Cannot be null] Valid message, a JSON object</param>
    /// <returns>
    /// The mutated messages, with a description
    /// </returns>
    internal static IEnumerable<(string Name, byte[] Bytes)> StructuralMutations(byte[] valid)
    {
        // Mutating each property
        string text = Encoding.UTF8.GetString(valid);
        JsonObject original = JsonNode.Parse(valid)!.AsObject();
        foreach (string[] path in ListPaths(original))
        {
            string key = path[^1];
            string name = string.Join('.', path);

            // Deleted
            (JsonObject root, JsonObject parent) = Reach(valid, path);
            JsonValueKind kind = KindOf(parent[key]);
            parent.Remove(key);
            yield return ($"delete {name}", ToBytes(root));

            // Renamed, then in another case
            (root, parent) = Reach(valid, path);
            JsonNode? value = parent[key];
            parent.Remove(key);
            parent[key + "X"] = value;
            yield return ($"rename {name}", ToBytes(root));
            (root, parent) = Reach(valid, path);
            value = parent[key];
            parent.Remove(key);
            parent[char.IsUpper(key[0]) ? char.ToLowerInvariant(key[0]) + key[1..] : char.ToUpperInvariant(key[0]) + key[1..]] = value;
            yield return ($"change the case of {name}", ToBytes(root));

            // Written twice, at the end of its object
            (root, parent) = Reach(valid, path);
            string valueText = parent[key]!.ToJsonString();
            string parentText = path.Length == 1 ? root.ToJsonString() : root[path[0]]!.ToJsonString();
            string duplicated = parentText[..^1] + ",\"" + key + "\":" + valueText + "}";
            if (!text.Contains(parentText, StringComparison.Ordinal))
                throw new InvalidOperationException($"The test message does not contain the object of {name} as the JSON node writes it");
            yield return ($"duplicate {name}", Encoding.UTF8.GetBytes(text.Replace(parentText, duplicated, StringComparison.Ordinal)));

            // Given a value of another kind
            foreach (string replacement in new[] { "7", "\"7\"", "\"x\"", "true", "null", "[]", "{}" })
            {
                if (KindOf(JsonNode.Parse(replacement)) == kind)
                    continue;
                (root, parent) = Reach(valid, path);
                parent[key] = JsonNode.Parse(replacement);
                yield return ($"replace {name} by {replacement}", ToBytes(root));
            }
        }
    }

    /// <summary>
    /// Method used to build the inputs that are not a JSON object holding a valid message, whatever the reader
    /// </summary>
    /// <param name="valid">[Cannot be null] Valid message</param>
    /// <returns>
    /// The inputs, with a description
    /// </returns>
    internal static IEnumerable<(string Name, byte[] Bytes)> OddDocuments(byte[] valid)
    {
        // Documents that are not the message
        yield return ("empty", []);
        yield return ("blank", Encoding.UTF8.GetBytes("  \n "));
        yield return ("null", Encoding.UTF8.GetBytes("null"));
        yield return ("number", Encoding.UTF8.GetBytes("7"));
        yield return ("string", Encoding.UTF8.GetBytes("\"text\""));
        yield return ("array", Encoding.UTF8.GetBytes("[]"));
        yield return ("empty object", Encoding.UTF8.GetBytes("{}"));
        yield return ("array of the message", [.. "["u8, .. valid, .. "]"u8]);
        yield return ("two messages", [.. valid, .. valid]);
        yield return ("trailing text", [.. valid, .. "x"u8]);
        yield return ("trailing comma", [.. valid[..^1], .. ",}"u8]);
        yield return ("byte order mark", [0xEF, 0xBB, 0xBF, .. valid]);
        yield return ("invalid UTF-8 prefix", [0xFF, .. valid]);
        yield return ("comment", [.. "/*x*/"u8, .. valid]);
    }

    /// <summary>
    /// Method used to build the messages that gain a property holding arrays nested too deeply
    /// </summary>
    /// <param name="valid">[Cannot be null] Valid message, a JSON object</param>
    /// <returns>
    /// The messages, with a description; the depths go from just over the maximum to one that would overflow a recursive parser
    /// </returns>
    internal static IEnumerable<(string Name, byte[] Bytes)> TooDeepDocuments(byte[] valid)
    {
        // Adding the property, which makes the message one level deep and the arrays the others
        foreach (int depth in new[] { 16, 17, 40, 1000, 100000 })
        {
            string deep = new string('[', depth) + new string(']', depth);
            yield return ($"{depth} nested arrays", [.. valid[..^1], .. Encoding.UTF8.GetBytes(",\"deep\":" + deep + "}")]);
        }
    }

    /// <summary>
    /// Method used to run a reader and check that it ends with a result or with one of the expected refusals, nothing else
    /// </summary>
    /// <param name="read">[Cannot be null] Reader to run</param>
    /// <param name="context">[Cannot be null] What is run, for the failure message</param>
    /// <param name="allowRemoteError">When true, a <see cref="RemoteErrorException"/> is also an expected outcome</param>
    internal static void AssertOnlyExpectedOutcomes(Action read, string context, bool allowRemoteError)
    {
        // Running
        Exception? exception = Record.Exception(read);

        // Check
        bool expected = exception is null or InvalidDataException || (allowRemoteError && exception is RemoteErrorException);
        expected.ShouldBeTrue($"{context}: unexpected {exception?.GetType().Name}: {exception?.Message}");
    }

    /// <summary>
    /// Method used to run a reader and check that it refuses its input with an <see cref="InvalidDataException"/>
    /// </summary>
    /// <param name="read">[Cannot be null] Reader to run</param>
    /// <param name="context">[Cannot be null] What is run, for the failure message</param>
    internal static void AssertRefused(Action read, string context)
    {
        // Running
        Exception? exception = Record.Exception(read);

        // Check
        exception.ShouldNotBeNull($"{context}: the input was accepted");
        (exception is InvalidDataException).ShouldBeTrue($"{context}: unexpected {exception.GetType().Name}: {exception.Message}");
    }

    /// <summary>
    /// Method used to run a reader on an input holding a marker, and check that no text of the exception holds the marker
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the whole exception is searched (message, inner exception, stack trace), not only its message; the marker is
    /// searched in pieces, split at the quote, the backslash and the control Characters, so that an escaped copy is found too.</para>
    /// </remarks>
    /// <param name="read">[Cannot be null] Reader to run</param>
    /// <param name="marker">[Cannot be null] Text of the peer that must never be copied</param>
    /// <param name="context">[Cannot be null] What is run, for the failure message</param>
    /// <param name="mustRefuse">When true, the input must also be refused with an <see cref="InvalidDataException"/></param>
    internal static void AssertNoPeerText(Action read, string marker, string context, bool mustRefuse)
    {
        // Running
        Exception? exception = Record.Exception(read);

        // Check: the refusal
        if (mustRefuse)
        {
            exception.ShouldNotBeNull($"{context}: the input was accepted");
            (exception is InvalidDataException).ShouldBeTrue($"{context}: unexpected {exception.GetType().Name}");
        }

        // Check: no text of the peer anywhere in the exception
        if (exception is not null)
        {
            string everything = exception.ToString();
            foreach (string piece in marker.Split('\u0007', '\n', '"', '\\'))
            {
                if (piece.Length >= 4)
                    everything.Contains(piece, StringComparison.Ordinal).ShouldBeFalse($"{context}: the exception copies the text of the peer");
            }
            everything.Contains('é').ShouldBeFalse($"{context}: the exception copies the text of the peer");
            exception.InnerException.ShouldBeNull($"{context}: an inner exception can carry the text of the peer");
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
