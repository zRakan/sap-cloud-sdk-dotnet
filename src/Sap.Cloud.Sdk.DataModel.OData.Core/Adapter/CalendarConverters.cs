using Sap.Cloud.Sdk.TypeConverter;

namespace Sap.Cloud.Sdk.DataModel.OData.Adapter;

/// <summary>Converts between <see cref="DateTime"/> (Java: <c>LocalDateTime</c>) and the instant of a custom field value.</summary>
/// <remarks>
/// Port of <c>LocalDateTimeCalendarConverter</c>. Java's <c>Calendar</c> in UTC is represented by a <see cref="DateTimeOffset"/> with
/// zero offset, which is also what custom fields of the form <c>/Date(ms)/</c> are read as.
/// </remarks>
public sealed class DateTimeCalendarConverter : AbstractTypeConverter<DateTime, DateTimeOffset>
{
    /// <inheritdoc />
    public override ConvertedObject<DateTimeOffset> ToDomainNonNull(DateTime obj) =>
        ConvertedObject<DateTimeOffset>.Of(new DateTimeOffset(DateTime.SpecifyKind(obj, DateTimeKind.Utc)));

    /// <inheritdoc />
    public override ConvertedObject<DateTime> FromDomainNonNull(DateTimeOffset domainObject) =>
        ConvertedObject<DateTime>.Of(DateTime.SpecifyKind(domainObject.DateTime, DateTimeKind.Unspecified));
}

/// <summary>Converts between <see cref="TimeOnly"/> (Java: <c>LocalTime</c>) and the instant of a custom field value.</summary>
/// <remarks>Port of <c>LocalTimeCalendarConverter</c>. The date part of the instant is 1970-01-01.</remarks>
public sealed class TimeOnlyCalendarConverter : AbstractTypeConverter<TimeOnly, DateTimeOffset>
{
    /// <inheritdoc />
    public override ConvertedObject<DateTimeOffset> ToDomainNonNull(TimeOnly obj) =>
        ConvertedObject<DateTimeOffset>.Of(new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(obj.Ticks)));

    /// <inheritdoc />
    public override ConvertedObject<TimeOnly> FromDomainNonNull(DateTimeOffset domainObject) =>
        ConvertedObject<TimeOnly>.Of(TimeOnly.FromTimeSpan(domainObject.TimeOfDay));
}

/// <summary>Converts between <see cref="DateTimeOffset"/> (Java: <c>ZonedDateTime</c>) and the instant of a custom field value.</summary>
/// <remarks>Port of <c>ZonedDateTimeCalendarConverter</c>; the offset is preserved.</remarks>
public sealed class DateTimeOffsetCalendarConverter : AbstractTypeConverter<DateTimeOffset, DateTimeOffset>
{
    /// <inheritdoc />
    public override ConvertedObject<DateTimeOffset> ToDomainNonNull(DateTimeOffset obj) => ConvertedObject<DateTimeOffset>.Of(obj);

    /// <inheritdoc />
    public override ConvertedObject<DateTimeOffset> FromDomainNonNull(DateTimeOffset domainObject) =>
        ConvertedObject<DateTimeOffset>.Of(domainObject);
}
