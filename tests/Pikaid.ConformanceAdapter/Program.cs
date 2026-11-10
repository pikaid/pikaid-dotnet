using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pikaid.ConformanceAdapter;

/// <summary>
/// Test-only JSON-Lines bridge for the official PikaID conformance suite.
/// Every operation delegates to the real <see cref="PikaidId"/> public API.
/// This project is not packed and is never referenced by the production library.
/// </summary>
internal static class Program
{
    private const string MutationVariable = "PIKAID_CONFORMANCE_MUTATION";
    private const string CorruptTextToBinaryMutation = "corrupt-text-to-binary";

    private static readonly string[] Capabilities =
    {
        "generate_string",
        "text_to_binary",
        "binary_to_text",
        "boolean_validation",
        "format_only_validation",
        "parse_components",
        "parse_datetime",
        "generate_with_timestamp",
        "generate_binary",
        "generate_binary_with_timestamp",
        "public_compare",
        "csprng_failure_injection",
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private static readonly string? Mutation =
        Environment.GetEnvironmentVariable(MutationVariable);

    private static bool s_faultActive;
    private static readonly IEntropySource s_throwingEntropy = new ThrowingEntropySource();

    private static int Main()
    {
        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();

        using StreamReader reader = new(
            input,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false);
        using StreamWriter writer = new(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            Handle(line, writer);
        }

        return 0;
    }

    private static void Handle(string line, StreamWriter writer)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            WriteError(writer, 0, "invalid_input", "request is not valid JSON");
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("request_id", out JsonElement idElement)
                || idElement.ValueKind != JsonValueKind.Number
                || !idElement.TryGetInt32(out int requestId))
            {
                WriteError(writer, 0, "invalid_input", "request must be an object with an integer request_id");
                return;
            }

            if (!root.TryGetProperty("operation", out JsonElement operationElement)
                || operationElement.ValueKind != JsonValueKind.String)
            {
                WriteError(writer, requestId, "invalid_input", "request must contain a string operation");
                return;
            }

            if (!root.TryGetProperty("arguments", out JsonElement arguments)
                || arguments.ValueKind != JsonValueKind.Object)
            {
                WriteError(writer, requestId, "invalid_input", "request must contain an object arguments value");
                return;
            }

            string operation = operationElement.GetString()!;
            try
            {
                object value = Dispatch(operation, arguments);
                WriteSuccess(writer, requestId, value);
            }
            catch (AdapterInputException exception)
            {
                WriteError(writer, requestId, "invalid_input", exception.Message);
            }
            catch (PikaidFormatException exception)
            {
                WriteError(writer, requestId, "invalid_input", exception.Message);
            }
            catch (UnsupportedOperationException exception)
            {
                WriteError(writer, requestId, "unsupported_operation", exception.Message);
            }
            catch (CryptographicException exception)
            {
                WriteError(writer, requestId, "entropy_unavailable", exception.Message);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    FormattableString.Invariant(
                        $"adapter internal error for operation {operation}: {exception}"));
                WriteError(writer, requestId, "internal_error", "unexpected adapter failure");
            }
        }
    }

    private static object Dispatch(string operation, JsonElement arguments) => operation switch
    {
        "hello" => Hello(),
        "generate_string" => PikaidId.New().ToString(),
        "text_to_binary" => TextToBinary(RequireString(arguments, "text")),
        "binary_to_text" => BinaryToText(RequireString(arguments, "binary_hex")),
        "boolean_validation" => PikaidId.IsValid(RequireString(arguments, "text")),
        "format_only_validation" => PikaidId.IsWellFormed(RequireString(arguments, "text")),
        "parse_components" => ParseComponents(RequireString(arguments, "text")),
        "generate_with_timestamp" => PikaidId.New(RequireTimestamp(arguments)).ToString(),
        "generate_binary" => Hex(PikaidId.NewBytes()),
        "generate_binary_with_timestamp" => Hex(PikaidId.NewBytes(RequireTimestamp(arguments))),
        "public_compare" => PikaidId.Parse(RequireString(arguments, "a"))
            .CompareTo(PikaidId.Parse(RequireString(arguments, "b"))),
        "inject_fault" => InjectFault(arguments),
        _ => throw new UnsupportedOperationException(
            FormattableString.Invariant($"operation '{operation}' is not implemented")),
    };

    private static Dictionary<string, object?> Hello() => new()
    {
        ["protocol_version"] = 1,
        ["capabilities"] = Capabilities,
    };

    private static string TextToBinary(string text)
    {
        byte[] bytes = PikaidId.Parse(text).ToByteArray();
        if (string.Equals(Mutation, CorruptTextToBinaryMutation, StringComparison.Ordinal))
        {
            bytes[^1] ^= 0x01;
        }

        return Hex(bytes);
    }

    private static string BinaryToText(string binaryHex)
    {
        if (binaryHex.Length != PikaidId.BinaryLength * 2)
        {
            throw new AdapterInputException(
                FormattableString.Invariant(
                    $"binary_hex must contain exactly {PikaidId.BinaryLength} bytes encoded as lowercase hex"));
        }

        for (int i = 0; i < binaryHex.Length; i++)
        {
            char character = binaryHex[i];
            bool isLowerHex = (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f');
            if (!isLowerHex)
            {
                throw new AdapterInputException("binary_hex must use lowercase hexadecimal digits");
            }
        }

        byte[] bytes = new byte[PikaidId.BinaryLength];
        for (int i = 0; i < bytes.Length; i++)
        {
            int high = DecodeLowerHex(binaryHex[i * 2]);
            int low = DecodeLowerHex(binaryHex[(i * 2) + 1]);
            bytes[i] = (byte)((high << 4) | low);
        }

        return PikaidId.FromBytes(bytes).ToString();
    }

    private static Dictionary<string, object?> ParseComponents(string text)
    {
        PikaidId id = PikaidId.Parse(text);
        return new Dictionary<string, object?>
        {
            ["timestamp_seconds"] = id.Timestamp,
            ["randomness_hex"] = id.RandomnessHex,
            ["datetime_utc"] = id.TimestampUtc.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture),
        };
    }

    private static Dictionary<string, object?> InjectFault(JsonElement arguments)
    {
        string fault = RequireString(arguments, "fault");
        if (!string.Equals(fault, "csprng_failure", StringComparison.Ordinal))
        {
            throw new AdapterInputException("only the csprng_failure fault is supported");
        }

        bool active = RequireBoolean(arguments, "active");
        if (active)
        {
            if (!s_faultActive)
            {
                PikaidEntropy.Source = s_throwingEntropy;
                s_faultActive = true;
            }
        }
        else if (s_faultActive)
        {
            PikaidEntropy.Source = CryptoEntropySource.Instance;
            s_faultActive = false;
        }

        return new Dictionary<string, object?>
        {
            ["fault"] = fault,
            ["active"] = active,
        };
    }

    private static string RequireString(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            throw new AdapterInputException(
                FormattableString.Invariant($"'{name}' must be a string"));
        }

        return element.GetString()!;
    }

    private static bool RequireBoolean(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out JsonElement element)
            || (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False))
        {
            throw new AdapterInputException(
                FormattableString.Invariant($"'{name}' must be a boolean"));
        }

        return element.GetBoolean();
    }

    private static ulong RequireTimestamp(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("timestamp_seconds", out JsonElement element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt64(out long timestamp)
            || timestamp < 0
            || timestamp > (long)PikaidId.MaxTimestamp)
        {
            throw new AdapterInputException(
                FormattableString.Invariant(
                    $"timestamp_seconds must be an integer between 0 and {PikaidId.MaxTimestamp}"));
        }

        return (ulong)timestamp;
    }

    private static int DecodeLowerHex(char character)
    {
        if (character >= '0' && character <= '9')
        {
            return character - '0';
        }

        return character - 'a' + 10;
    }

    private static string Hex(byte[] bytes)
    {
        const string digits = "0123456789abcdef";
        char[] characters = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            characters[i * 2] = digits[bytes[i] >> 4];
            characters[(i * 2) + 1] = digits[bytes[i] & 0x0F];
        }

        return new string(characters);
    }

    private static void WriteSuccess(StreamWriter writer, int requestId, object value)
    {
        writer.WriteLine(JsonSerializer.Serialize(
            new
            {
                request_id = requestId,
                ok = true,
                value,
            },
            SerializerOptions));
    }

    private static void WriteError(StreamWriter writer, int requestId, string category, string message)
    {
        writer.WriteLine(JsonSerializer.Serialize(
            new
            {
                request_id = requestId,
                ok = false,
                error = new
                {
                    category,
                    message,
                },
            },
            SerializerOptions));
    }

    private sealed class AdapterInputException : Exception
    {
        internal AdapterInputException(string message)
            : base(message)
        {
        }
    }

    private sealed class UnsupportedOperationException : Exception
    {
        internal UnsupportedOperationException(string message)
            : base(message)
        {
        }
    }

    private sealed class ThrowingEntropySource : IEntropySource
    {
        public void Fill(Span<byte> destination) =>
            throw new CryptographicException("injected CSPRNG failure");
    }
}
