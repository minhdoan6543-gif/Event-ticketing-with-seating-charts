namespace EventTicketing.Api.SeatMaps;

public sealed record SeatMapImportResult(int SeatCount, int CreatedCategoryCount, bool ReplacedExistingMap);
