namespace EventTicketing.Api.Models.SeatingChart;

public class RowDto
{
    public string? RowNumber { get; set; }
    public List<SeatDto>? Seats { get; set; }
}

