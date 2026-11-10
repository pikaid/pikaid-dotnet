using System;

namespace Pikaid;

/// <summary>
/// The exception thrown when a textual or binary value is not a valid pikaid.
/// </summary>
/// <remarks>
/// <see cref="PikaidId.Parse(string)"/> and <see cref="PikaidId.FromBytes(System.ReadOnlySpan{byte})"/>
/// throw this exception for malformed input. It derives from <see cref="FormatException"/>
/// so callers that already handle <see cref="FormatException"/> keep working.
/// </remarks>
public sealed class PikaidFormatException : FormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PikaidFormatException"/> class.
    /// </summary>
    public PikaidFormatException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PikaidFormatException"/> class
    /// with the specified message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PikaidFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PikaidFormatException"/> class
    /// with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public PikaidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
