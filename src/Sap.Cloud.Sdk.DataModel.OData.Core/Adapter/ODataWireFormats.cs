using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>
/// The OData V2 JSON representations of temporal and numeric values: <c>/Date(ms)/</c>, <c>/Date(ms+offset)/</c>,
/// <c>PT13H20M0S</c> and Java-style number strings.
/// </summary>
/// <remarks>
/// Port of the logic in <c>ODataDateTimeStringCalendarConverter</c>, <c>ODataDateTimeOffsetStringCalendarConverter</c>,
/// <c>ODataTimeStringCalendarConverter</c> and <c>ODataNumberSerializer</c>. Java's intermediate <c>Calendar</c> is not needed.
/// </remarks>
internal static partial class ODataWireFormats
{
    private const long TicksPerMillisecond = TimeSpan.TicksPerMillisecond;
    private static readonly long EpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    [GeneratedRegex(@"^/Date\((-?\d+)\)/$", RegexOptions.CultureInvariant)]
    internal static partial Regex DatePattern();

    [GeneratedRegex(@"^/Date\((-?\d+)(?:(\+|-)(\d{1,4}))?\)/$", RegexOptions.CultureInvariant)]
    private static partial Regex DateOffsetPattern();

    [GeneratedRegex(
        @"^P(?:(\d{1,2})Y)?(?:(\d{1,2})M)?(?:(\d{1,2})D)?T(?:(\d{1,2})H)?(?:(\d{1,4})M)?(?:(\d{1,5})(?:\.(\d+?)0*)?S)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimePattern();

    private static long FloorDiv(long a, long b) => (a / b) - ((a % b != 0 && ((a < 0) != (b < 0))) ? 1 : 0);

    /// <summary>Milliseconds since 1970-01-01T00:00:00Z of a ticks value counted from 0001-01-01.</summary>
    private static long ToEpochMilliseconds(long ticks) => FloorDiv(ticks - EpochTicks, TicksPerMillisecond);

    internal static string FormatDateTime(DateTime value) => "/Date(" + ToEpochMilliseconds(value.Ticks).ToString(CultureInfo.InvariantCulture) + ")/";

    internal static bool TryParseDateTime(string text, out DateTime value)
    {
        value = default;
        var match = DatePattern().Match(text);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var millis))
        {
            return false;
        }
        return TryFromMilliseconds(millis, out value);
    }

    private static bool TryFromMilliseconds(long millis, out DateTime value)
    {
        value = default;
        try
        {
            value = new DateTime(EpochTicks + (millis * TicksPerMillisecond), DateTimeKind.Unspecified);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal static string FormatDateTimeOffset(DateTimeOffset value)
    {
        var offsetMinutes = (long)value.Offset.TotalMinutes;
        var millis = ToEpochMilliseconds(value.UtcTicks);
        if (offsetMinutes == 0)
        {
            return "/Date(" + millis.ToString(CultureInfo.InvariantCulture) + ")/";
        }
        // The payload carries the local time in milliseconds followed by the offset in minutes.
        var local = millis + (offsetMinutes * 60_000);
        var sign = offsetMinutes < 0 ? "-" : "+";
        return "/Date(" + local.ToString(CultureInfo.InvariantCulture) + sign + Math.Abs(offsetMinutes).ToString("D4", CultureInfo.InvariantCulture) + ")/";
    }

    internal static bool TryParseDateTimeOffset(string text, out DateTimeOffset value)
    {
        value = default;
        var match = DateOffsetPattern().Match(text);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var millis))
        {
            return false;
        }
        var offset = TimeSpan.Zero;
        if (match.Groups[2].Success)
        {
            var offsetMinutes = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (offsetMinutes >= 24 * 60)
            {
                return false;
            }
            if (offsetMinutes != 0)
            {
                var sign = match.Groups[2].Value == "+" ? 1 : -1;
                offset = TimeSpan.FromMinutes(sign * offsetMinutes);
                // Convert the local-time milliseconds to UTC.
                millis -= sign * offsetMinutes * 60_000L;
            }
        }
        try
        {
            value = new DateTimeOffset(EpochTicks + (millis * TicksPerMillisecond), TimeSpan.Zero).ToOffset(offset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal static string FormatTime(TimeOnly value)
    {
        var result = new StringBuilder("PT", 21);
        result.Append(value.Hour.ToString(CultureInfo.InvariantCulture)).Append('H');
        result.Append(value.Minute.ToString(CultureInfo.InvariantCulture)).Append('M');
        result.Append(value.Second.ToString(CultureInfo.InvariantCulture));
        var millis = value.Millisecond;
        if (millis > 0)
        {
            result.Append('.').Append(millis.ToString("D3", CultureInfo.InvariantCulture).TrimEnd('0'));
        }
        return result.Append('S').ToString();
    }

    internal static bool TryParseTime(string text, out TimeOnly value)
    {
        value = default;
        var match = TimePattern().Match(text);
        if (!match.Success || !Enumerable.Range(1, 6).Any(i => match.Groups[i].Success))
        {
            return false;
        }
        var hours = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = match.Groups[5].Success ? int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) : 0;
        var seconds = match.Groups[6].Success ? int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : 0;
        var millis = 0;
        if (match.Groups[7].Success)
        {
            var decimals = match.Groups[7].Value;
            if (decimals.Length > 9)
            {
                return false;
            }
            var nanos = int.Parse(decimals.PadRight(9, '0'), CultureInfo.InvariantCulture);
            if (nanos % 1_000_000 != 0)
            {
                // The precision cannot be represented in milliseconds.
                return false;
            }
            millis = nanos / 1_000_000;
        }
        // Like java.util.Calendar, fields beyond their range roll over into the next unit.
        var totalSeconds = ((hours * 3600L) + (minutes * 60L) + seconds) % 86_400;
        value = new TimeOnly((totalSeconds * TimeSpan.TicksPerSecond) + (millis * TicksPerMillisecond));
        return true;
    }

    /// <summary>Formats a double the way <c>Double.toString</c> does in Java (for example <c>1.0</c>, <c>1.0E10</c>).</summary>
    internal static string FormatDouble(double value) => FormatFloatingPoint(value, value.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>Formats a float the way <c>Float.toString</c> does in Java.</summary>
    internal static string FormatSingle(float value) => FormatFloatingPoint(value, value.ToString("R", CultureInfo.InvariantCulture));

    private static string FormatFloatingPoint(double value, string roundTrip)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }
        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }
        if (value == 0)
        {
            return double.IsNegative(value) ? "-0.0" : "0.0";
        }
        var negative = roundTrip.StartsWith('-');
        var unsigned = negative ? roundTrip[1..] : roundTrip;
        // Normalize to digits and a decimal exponent: value = 0.DIGITS * 10^exponent
        var exponentPart = 0;
        var eIndex = unsigned.IndexOfAny(['E', 'e']);
        if (eIndex >= 0)
        {
            exponentPart = int.Parse(unsigned[(eIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            unsigned = unsigned[..eIndex];
        }
        var dot = unsigned.IndexOf('.', StringComparison.Ordinal);
        var intPart = dot >= 0 ? unsigned[..dot] : unsigned;
        var fracPart = dot >= 0 ? unsigned[(dot + 1)..] : string.Empty;
        var digits = (intPart + fracPart).TrimStart('0');
        var leadingZerosRemoved = (intPart + fracPart).Length - digits.Length;
        var exponent = intPart.Length - leadingZerosRemoved + exponentPart;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }
        var abs = Math.Abs(value);
        string result;
        if (abs is >= 1e-3 and < 1e7)
        {
            if (exponent <= 0)
            {
                result = "0." + new string('0', -exponent) + digits;
            }
            else if (digits.Length <= exponent)
            {
                result = digits + new string('0', exponent - digits.Length) + ".0";
            }
            else
            {
                result = digits[..exponent] + "." + digits[exponent..];
            }
        }
        else
        {
            var mantissa = digits[..1] + "." + (digits.Length > 1 ? digits[1..] : "0");
            result = mantissa + "E" + (exponent - 1).ToString(CultureInfo.InvariantCulture);
        }
        return negative ? "-" + result : result;
    }
}
