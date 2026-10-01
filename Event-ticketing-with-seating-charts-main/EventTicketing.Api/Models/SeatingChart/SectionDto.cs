namespace EventTicketing.Api.Models.SeatingChart;

public class SectionDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal? DefaultPrice { get; set; }
    public List<RowDto>? Rows { get; set; }
    public List<SeatDto>? Seats { get; set; }
}

