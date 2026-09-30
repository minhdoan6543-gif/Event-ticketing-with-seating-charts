using EventTicketing.Api.Models.SeatingChart;

namespace EventTicketing.Api.Services;

public interface ISeatingChartValidator
{
    Task<SeatingChartValidationResult> ValidateJsonStreamAsync(Stream utf8JsonStream, CancellationToken cancellationToken = default);
    SeatingChartValidationResult ValidateJsonString(string jsonContent);
    SeatingChartValidationResult ValidateDto(SeatingChartFileDto dto);
}

