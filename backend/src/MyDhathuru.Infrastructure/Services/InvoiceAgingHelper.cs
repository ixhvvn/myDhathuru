namespace MyDhathuru.Infrastructure.Services;

internal static class InvoiceAgingHelper
{
    private static readonly TimeSpan MaldivesOffset = TimeSpan.FromHours(5);

    public static DateOnly GetToday()
    {
        var maldivesNow = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        return DateOnly.FromDateTime(maldivesNow.DateTime);
    }

    public static InvoiceAgingSnapshot Evaluate(DateOnly dueDate, decimal balance, DateOnly? today = null)
    {
        var currentDate = today ?? GetToday();
        if (balance <= 0m || dueDate >= currentDate)
        {
            return InvoiceAgingSnapshot.None;
        }

        var daysOverdue = currentDate.DayNumber - dueDate.DayNumber;
        return new InvoiceAgingSnapshot(true, daysOverdue, ResolveBucket(daysOverdue));
    }

    private static string ResolveBucket(int daysOverdue)
    {
        if (daysOverdue > 90)
        {
            return "Over 90 days";
        }

        if (daysOverdue > 60)
        {
            return "Over 60 days";
        }

        if (daysOverdue > 30)
        {
            return "Over 30 days";
        }

        return "Past due";
    }
}

internal readonly record struct InvoiceAgingSnapshot(bool IsOverdue, int DaysOverdue, string? Bucket)
{
    public static InvoiceAgingSnapshot None => new(false, 0, null);
}
