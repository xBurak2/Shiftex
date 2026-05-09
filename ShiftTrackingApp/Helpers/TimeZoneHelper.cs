using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiftTrackingApp.Helpers
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo _tz =
            TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

        public static DateTime ConvertToTurkeyTime(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

        public static DateTime ConvertToUtc(DateTime local)
            => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz);
    }

    public class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private const string Format = "yyyy-MM-dd";

        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
            => DateOnly.ParseExact(reader.GetString()!, Format);

        public override void Write(Utf8JsonWriter writer, DateOnly value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(Format));
    }
}
