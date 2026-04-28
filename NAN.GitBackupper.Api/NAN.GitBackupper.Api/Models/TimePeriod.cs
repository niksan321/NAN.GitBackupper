using NAN.GitBackupper.Api.Models.Enums;

namespace NAN.GitBackupper.Api.Models;

public sealed class TimePeriod
{
    public TimePeriod()
    {
    }

    public TimePeriod(TimePeriodType type, int value)
    {
        Type = type;
        Value = value;
    }

    public int Value { get; set; }

    public TimePeriodType Type { get; set; }

    public TimeSpan ToTimeSpan()
    {
        return Type switch
        {
            TimePeriodType.Milliseconds => TimeSpan.FromMilliseconds(Value),
            TimePeriodType.Seconds => TimeSpan.FromSeconds(Value),
            TimePeriodType.Minutes => TimeSpan.FromMinutes(Value),
            TimePeriodType.Hours => TimeSpan.FromHours(Value),
            TimePeriodType.Days => TimeSpan.FromDays(Value),
            _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null),
        };
    }
}
