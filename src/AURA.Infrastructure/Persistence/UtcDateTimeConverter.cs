using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AURA.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            toProvider => toProvider.ToUniversalTime(),
            fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Utc))
    {
    }
}
