using System;

namespace Pikaid;

/// <summary>
/// Supplies the wall-clock reading used by <see cref="PikaidId.New()"/>.
/// Internal so tests can exercise the fail-closed clock-range checks without
/// changing the production behavior, which always reads the system UTC clock.
/// </summary>
internal static class PikaidClock
{
    private static Func<long> _unixSecondsProvider =
        static () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    internal static Func<long> UnixSecondsProvider
    {
        get => _unixSecondsProvider;
        set => _unixSecondsProvider = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static long UtcNowUnixSeconds() => _unixSecondsProvider();
}
