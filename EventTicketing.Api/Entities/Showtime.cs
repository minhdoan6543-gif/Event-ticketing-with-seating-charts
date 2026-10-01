namespace EventTicketing.Api.Entities;

public class Showtime
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ShowtimeStatus Status { get; set; } = ShowtimeStatus.Draft;

    public Event Event { get; set; } = null!;
}
