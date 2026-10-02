namespace EventTicketing.Api.Models.Events;

public class PublicEventItemDto
{
    public int EventId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Location { get; set; }
    public string? ImageUrl { get; set; }
    public PublicNearestShowtimeDto? NearestShowtime { get; set; }
    public int ShowtimeCount { get; set; }
    public bool HasAvailableShowtimes { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

public class PublicNearestShowtimeDto
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
}

public class PublicEventListResponse
{
    public required List<PublicEventItemDto> Items { get; set; }
    public string? NextCursor { get; set; }
}

public class PublicShowtimeDetailDto
{
    public int Id { get; set; }
    public bool IsOnSale { get; set; }
    public int? EventId { get; set; }
    public string? EventName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
