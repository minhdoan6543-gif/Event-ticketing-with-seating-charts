namespace EventTicketing.Api.Models.SeatingChart;

public class SeatingChartValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public string Message { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int TotalSections { get; set; }
    public int TotalRows { get; set; }
    public List<SeatingChartValidationError> Errors { get; set; } = new();

    public static SeatingChartValidationResult Success(
        int totalSeats,
        int totalSections,
        int totalRows,
        string message = "Tệp sơ đồ hợp lệ.")
    {
        return new SeatingChartValidationResult
        {
            Message = message,
            TotalSeats = totalSeats,
            TotalSections = totalSections,
            TotalRows = totalRows,
            Errors = new List<SeatingChartValidationError>()
        };
    }

    public static SeatingChartValidationResult Failure(
        List<SeatingChartValidationError> errors,
        string? message = null)
    {
        var count = errors.Count;
        return new SeatingChartValidationResult
        {
            Message = message ?? $"Tệp sơ đồ không hợp lệ và bị từ chối toàn bộ. Phát hiện {count} lỗi cần xử lý.",
            TotalSeats = 0,
            TotalSections = 0,
            TotalRows = 0,
            Errors = errors
        };
    }

    public static SeatingChartValidationResult SingleError(
        string location,
        string path,
        string errorCode,
        string errorMessage)
    {
        return Failure(new List<SeatingChartValidationError>
        {
            new(location, path, errorCode, errorMessage)
        });
    }
}

