namespace TicketBooking.Api.Models.SeatingChart;

public class SeatingChartFileDto
{
    public string? LayoutName { get; set; }
    public string? Venue { get; set; }
    public int? TotalCapacity { get; set; }
    public List<SectionDto>? Sections { get; set; }
    public List<SeatDto>? Seats { get; set; }
}

