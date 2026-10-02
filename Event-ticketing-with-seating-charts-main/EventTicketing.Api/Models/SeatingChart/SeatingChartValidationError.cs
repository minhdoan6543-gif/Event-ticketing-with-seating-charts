namespace EventTicketing.Api.Models.SeatingChart;

public class SeatingChartValidationError
{
    public string Location { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public SeatingChartValidationError()
    {
    }

    public SeatingChartValidationError(string location, string path, string errorCode, string message)
    {
        Location = location;
        Path = path;
        ErrorCode = errorCode;
        Message = message;
    }
}

