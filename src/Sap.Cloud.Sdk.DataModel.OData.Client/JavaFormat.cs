using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Client;

/// <summary>
/// Formats and parses values exactly as the Java <c>toString</c>/<c>parse</c> implementations do, so that URLs and
/// payloads produced by this library are identical to the ones of the Java SDK.
/// </summary>
internal static partial class JavaFormat
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;
    private const long NanosPerTick = 100;
    private const long NanosPerSecond = 1_000_000_000;

    /// <summary>Equivalent of <c>Double.toString</c>.</summary>
    internal static string ToString(double value)
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
        return FromRoundTrip(value.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>Equivalent of <c>Float.toString</c>.</summary>
    internal static string ToString(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }
        if (float.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }
        if (value == 0)
        {
            return float.IsNegative(value) ? "-0.0" : "0.0";
        }
        return FromRoundTrip(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static string FromRoundTrip(string roundTrip)
    {
        var negative = roundTrip.StartsWith('-');
        var s = negative ? roundTrip[1..] : roundTrip;
        var exponent = 0;
        var e = s.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            exponent = int.Parse(s[(e + 1)..], CultureInfo.InvariantCulture);
            s = s[..e];
        }
        var dot = s.IndexOf('.', StringComparison.Ordinal);
        var digits = dot >= 0 ? s.Remove(dot, 1) : s;
        var pointPosition = dot >= 0 ? dot : s.Length;
        var leadingZeros = digits.Length - digits.TrimStart('0').Length;
        digits = digits.TrimStart('0');
        pointPosition -= leadingZeros;
        digits = digits.TrimEnd('0');
        var scientificExponent = pointPosition - 1 + exponent;

        var sb = new StringBuilder();
        if (negative)
        {
            sb.Append('-');
        }
        if (scientificExponent is >= -3 and < 7)
        {
            if (scientificExponent >= 0)
            {
                var integerLength = scientificExponent + 1;
                sb.Append(digits.Length >= integerLength ? digits[..integerLength] : digits.PadRight(integerLength, '0'));
                sb.Append('.');
                sb.Append(digits.Length > integerLength ? digits[integerLength..] : "0");
            }
            else
            {
                sb.Append("0.");
                sb.Append('0', -scientificExponent - 1);
                sb.Append(digits);
            }
        }
        else
        {
            sb.Append(digits[0]);
            sb.Append('.');
            sb.Append(digits.Length > 1 ? digits[1..] : "0");
            sb.Append('E');
            sb.Append(scientificExponent.ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>Equivalent of <c>Duration.toString</c> (ISO-8601, hours as the largest unit).</summary>
    internal static string ToIsoString(TimeSpan duration) => ToIsoString(duration.Ticks);

    internal static string ToIsoString(long ticks)
    {
        if (ticks == 0)
        {
            return "PT0S";
        }
        var seconds = Math.DivRem(ticks, TicksPerSecond, out var remainder);
        if (remainder < 0)
        {
            seconds--;
            remainder += TicksPerSecond;
        }
        var nanos = remainder * NanosPerTick;

        var effectiveTotalSeconds = seconds;
        if (seconds < 0 && nanos > 0)
        {
            effectiveTotalSeconds++;
        }
        var hours = effectiveTotalSeconds / 3600;
        var minutes = (int)(effectiveTotalSeconds % 3600 / 60);
        var secs = (int)(effectiveTotalSeconds % 60);
        var buf = new StringBuilder("PT");
        if (hours != 0)
        {
            buf.Append(hours.ToString(CultureInfo.InvariantCulture)).Append('H');
        }
        if (minutes != 0)
        {
            buf.Append(minutes.ToString(CultureInfo.InvariantCulture)).Append('M');
        }
        if (secs == 0 && nanos == 0 && buf.Length > 2)
        {
            return buf.ToString();
        }
        if (seconds < 0 && nanos > 0)
        {
            buf.Append(secs == 0 ? "-0" : secs.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            buf.Append(secs.ToString(CultureInfo.InvariantCulture));
        }
        if (nanos > 0)
        {
            var pos = buf.Length;
            buf.Append((seconds < 0 ? 2 * NanosPerSecond - nanos : nanos + NanosPerSecond).ToString(CultureInfo.InvariantCulture));
            while (buf[^1] == '0')
            {
                buf.Length--;
            }
            buf[pos] = '.';
        }
        buf.Append('S');
        return buf.ToString();
    }

    [GeneratedRegex(@"^([-+]?)P(?:([-+]?[0-9]+)D)?(T(?:([-+]?[0-9]+)H)?(?:([-+]?[0-9]+)M)?(?:([-+]?[0-9]+)(?:[.,]([0-9]{0,9}))?S)?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    /// <summary>Equivalent of <c>Duration.parse</c>.</summary>
    internal static bool TryParseDuration(string text, out TimeSpan duration)
    {
        duration = default;
        var m = DurationPattern().Match(text);
        if (!m.Success || (m.Groups[3].Success && m.Groups[3].Value.Equals("T", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        if (!m.Groups[2].Success && !m.Groups[4].Success && !m.Groups[5].Success && !m.Groups[6].Success)
        {
            return false;
        }
        try
        {
            checked
            {
                var negate = m.Groups[1].Value == "-";
                long ticks = 0;
                ticks += ParseLong(m.Groups[2]) * 86400 * TicksPerSecond;
                ticks += ParseLong(m.Groups[4]) * 3600 * TicksPerSecond;
                ticks += ParseLong(m.Groups[5]) * 60 * TicksPerSecond;
                var secondsText = m.Groups[6];
                var seconds = ParseLong(secondsText);
                var negativeSeconds = secondsText.Success && secondsText.Value.StartsWith('-');
                ticks += seconds * TicksPerSecond;
                if (m.Groups[7].Success && m.Groups[7].Value.Length > 0)
                {
                    var fraction = m.Groups[7].Value.PadRight(9, '0');
                    var nanos = long.Parse(fraction, CultureInfo.InvariantCulture);
                    var fractionTicks = nanos / NanosPerTick;
                    ticks += negativeSeconds ? -fractionTicks : fractionTicks;
                }
                duration = TimeSpan.FromTicks(negate ? -ticks : ticks);
                return true;
            }
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static long ParseLong(Group g) => g.Success ? long.Parse(g.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;

    /// <summary>Equivalent of <c>LocalTime.toString</c>.</summary>
    internal static string ToString(TimeOnly time)
    {
        var sb = new StringBuilder();
        sb.Append(time.Hour.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(time.Minute.ToString("00", CultureInfo.InvariantCulture));
        var nano = time.Ticks % TicksPerSecond * NanosPerTick;
        if (time.Second > 0 || nano > 0)
        {
            sb.Append(':').Append(time.Second.ToString("00", CultureInfo.InvariantCulture));
            if (nano > 0)
            {
                sb.Append('.');
                if (nano % 1_000_000 == 0)
                {
                    sb.Append((nano / 1_000_000 + 1000).ToString(CultureInfo.InvariantCulture).AsSpan(1));
                }
                else if (nano % 1000 == 0)
                {
                    sb.Append((nano / 1000 + 1_000_000).ToString(CultureInfo.InvariantCulture).AsSpan(1));
                }
                else
                {
                    sb.Append((nano + NanosPerSecond).ToString(CultureInfo.InvariantCulture).AsSpan(1));
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>Equivalent of formatting with <c>DateTimeFormatter.ISO_LOCAL_TIME</c>.</summary>
    internal static string ToIsoLocalTime(TimeOnly time)
    {
        var sb = new StringBuilder();
        sb.Append(time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        AppendFraction(sb, time.Ticks % TicksPerSecond);
        return sb.ToString();
    }

    private static void AppendFraction(StringBuilder sb, long fractionTicks)
    {
        if (fractionTicks == 0)
        {
            return;
        }
        sb.Append('.').Append((fractionTicks * NanosPerTick).ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0'));
    }

    /// <summary>Equivalent of formatting with <c>DateTimeFormatter.ISO_LOCAL_DATE</c>.</summary>
    internal static string ToIsoLocalDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string ToOffsetId(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return "Z";
        }
        var abs = offset.Duration();
        return (offset < TimeSpan.Zero ? "-" : "+") + abs.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + abs.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Equivalent of formatting with <c>DateTimeFormatter.ISO_OFFSET_DATE_TIME</c>.</summary>
    internal static string ToIsoOffsetDateTime(DateTimeOffset value)
    {
        var sb = new StringBuilder();
        sb.Append(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        AppendFraction(sb, value.Ticks % TicksPerSecond);
        sb.Append(ToOffsetId(value.Offset));
        return sb.ToString();
    }

    /// <summary>Equivalent of <c>OffsetDateTime.toString</c> (seconds are omitted when zero).</summary>
    internal static string ToString(DateTimeOffset value) =>
        ToIsoLocalDate(DateOnly.FromDateTime(value.DateTime)) + "T" + ToString(TimeOnly.FromDateTime(value.DateTime)) + ToOffsetId(value.Offset);

    /// <summary>Formats a local date-time for the OData V2 <c>datetime'…'</c> literal (fraction of at most 7 digits).</summary>
    internal static string ToEdmDateTime(DateTime value)
    {
        var sb = new StringBuilder();
        sb.Append(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        AppendFraction(sb, value.Ticks % TicksPerSecond);
        return sb.ToString();
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}([.,]\d{1,9})?)?(Z|[+-]\d{2}(:?\d{2}(:?\d{2})?)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetDateTimePattern();

    /// <summary>Equivalent of <c>OffsetDateTime.parse</c>.</summary>
    internal static bool TryParseOffsetDateTime(string text, out DateTimeOffset value)
    {
        value = default;
        if (!OffsetDateTimePattern().IsMatch(text))
        {
            return false;
        }
        return DateTimeOffset.TryParse(text.Replace(',', '.'), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
    }

    /// <summary>Equivalent of <c>LocalDate.parse</c>.</summary>
    internal static bool TryParseDate(string text, out DateOnly value) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    /// <summary>Equivalent of <c>LocalTime.parse</c>.</summary>
    internal static bool TryParseTime(string text, out TimeOnly value) =>
        TimeOnly.TryParseExact(text, ["HH:mm", "HH:mm:ss", "HH:mm:ss.f", "HH:mm:ss.ff", "HH:mm:ss.fff", "HH:mm:ss.ffff", "HH:mm:ss.fffff", "HH:mm:ss.ffffff", "HH:mm:ss.fffffff"], CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
}
