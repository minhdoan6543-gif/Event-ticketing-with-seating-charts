using System.Text.Json;
using TicketBooking.Api.Models.SeatingChart;

namespace TicketBooking.Api.Services;

public class SeatingChartValidator : ISeatingChartValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available",
        "Reserved",
        "Booked",
        "Blocked",
        "Disabled",
        "Locked",
        "Accessible"
    };

    public async Task<SeatingChartValidationResult> ValidateJsonStreamAsync(
        Stream utf8JsonStream,
        CancellationToken cancellationToken = default)
    {
        if (utf8JsonStream == null)
        {
            return SeatingChartValidationResult.SingleError(
                "Tệp tải lên (File)",
                "root",
                "EMPTY_FILE",
                "Tệp sơ đồ rỗng hoặc không có luồng dữ liệu.");
        }

        if (utf8JsonStream.CanSeek && utf8JsonStream.Length == 0)
        {
            return SeatingChartValidationResult.SingleError(
                "Tệp tải lên (File)",
                "root",
                "EMPTY_FILE",
                "Tệp sơ đồ rỗng (0 bytes).");
        }

        try
        {
            var dto = await JsonSerializer.DeserializeAsync<SeatingChartFileDto>(
                utf8JsonStream,
                JsonOptions,
                cancellationToken);

            if (dto == null)
            {
                return SeatingChartValidationResult.SingleError(
                    "Gốc tệp (Root)",
                    "root",
                    "INVALID_ROOT_OBJECT",
                    "Cấu trúc gốc phải là một đối tượng JSON hợp lệ chứa thông tin sơ đồ.");
            }

            return ValidateDto(dto);
        }
        catch (JsonException ex)
        {
            var lineNumber = ex.LineNumber ?? 1;
            var bytePos = ex.BytePositionInLine ?? 0;
            var location = $"Dòng {lineNumber}, Cột {bytePos}";
            var path = string.IsNullOrWhiteSpace(ex.Path) ? "root" : ex.Path;

            return SeatingChartValidationResult.SingleError(
                location,
                path,
                "INVALID_JSON_SYNTAX",
                $"Lỗi cú pháp JSON tại dòng {lineNumber}, cột {bytePos}: {ex.Message}");
        }
    }

    public SeatingChartValidationResult ValidateJsonString(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return SeatingChartValidationResult.SingleError(
                "Tệp tải lên (File)",
                "root",
                "EMPTY_FILE",
                "Tệp sơ đồ rỗng hoặc không có nội dung văn bản.");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<SeatingChartFileDto>(jsonContent, JsonOptions);
            if (dto == null)
            {
                return SeatingChartValidationResult.SingleError(
                    "Gốc tệp (Root)",
                    "root",
                    "INVALID_ROOT_OBJECT",
                    "Cấu trúc gốc phải là một đối tượng JSON hợp lệ chứa thông tin sơ đồ.");
            }

            return ValidateDto(dto);
        }
        catch (JsonException ex)
        {
            var lineNumber = ex.LineNumber ?? 1;
            var bytePos = ex.BytePositionInLine ?? 0;
            var location = $"Dòng {lineNumber}, Cột {bytePos}";
            var path = string.IsNullOrWhiteSpace(ex.Path) ? "root" : ex.Path;

            return SeatingChartValidationResult.SingleError(
                location,
                path,
                "INVALID_JSON_SYNTAX",
                $"Lỗi cú pháp JSON tại dòng {lineNumber}, cột {bytePos}: {ex.Message}");
        }
    }

    public SeatingChartValidationResult ValidateDto(SeatingChartFileDto dto)
    {
        var errors = new List<SeatingChartValidationError>();

        if (string.IsNullOrWhiteSpace(dto.LayoutName))
        {
            errors.Add(new SeatingChartValidationError(
                "Tên sơ đồ (layoutName)",
                "layoutName",
                "MISSING_LAYOUT_NAME",
                "Tên sơ đồ (layoutName) là bắt buộc và không được để trống."));
        }

        var hasSections = dto.Sections != null && dto.Sections.Count > 0;
        var hasFlatSeats = dto.Seats != null && dto.Seats.Count > 0;

        if (!hasSections && !hasFlatSeats)
        {
            errors.Add(new SeatingChartValidationError(
                "Dữ liệu ghế (seats/sections)",
                "sections",
                "EMPTY_SEATING_DATA",
                "Sơ đồ phải chứa ít nhất một khu vực (sections) hoặc danh sách ghế (seats)."));
        }

        var sectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seatIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var coordinatesMap = new Dictionary<(double X, double Y), string>();

        var distinctSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalSeatsCounted = 0;

        if (dto.Sections != null)
        {
            for (var sIdx = 0; sIdx < dto.Sections.Count; sIdx++)
            {
                var section = dto.Sections[sIdx];
                if (section == null)
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Khu vực thứ {sIdx + 1}",
                        $"sections[{sIdx}]",
                        "NULL_SECTION",
                        $"Khu vực tại vị trí {sIdx + 1} có giá trị rỗng (null)."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(section.Name))
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Khu vực thứ {sIdx + 1}",
                        $"sections[{sIdx}].name",
                        "MISSING_SECTION_NAME",
                        $"Khu vực tại vị trí {sIdx + 1} không có tên (name)."));
                }
                else if (!sectionNames.Add(section.Name.Trim()))
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Khu vực '{section.Name}'",
                        $"sections[{sIdx}].name",
                        "DUPLICATE_SECTION_NAME",
                        $"Tên khu vực '{section.Name}' bị trùng lặp trong sơ đồ."));
                }

                var sectionName = string.IsNullOrWhiteSpace(section.Name) ? $"KhuVuc_{sIdx + 1}" : section.Name.Trim();
                distinctSections.Add(sectionName);

                var hasRows = section.Rows != null && section.Rows.Count > 0;
                var hasSecSeats = section.Seats != null && section.Seats.Count > 0;

                if (!hasRows && !hasSecSeats)
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Khu vực '{sectionName}'",
                        $"sections[{sIdx}]",
                        "EMPTY_SECTION",
                        $"Khu vực '{sectionName}' không chứa bất kỳ hàng ghế hoặc ghế nào."));
                    continue;
                }

                if (section.Rows != null)
                {
                    for (var rIdx = 0; rIdx < section.Rows.Count; rIdx++)
                    {
                        var row = section.Rows[rIdx];
                        if (row == null)
                        {
                            errors.Add(new SeatingChartValidationError(
                                $"Khu vực '{sectionName}', Hàng thứ {rIdx + 1}",
                                $"sections[{sIdx}].rows[{rIdx}]",
                                "NULL_ROW",
                                $"Hàng ghế tại vị trí {rIdx + 1} thuộc khu vực '{sectionName}' có giá trị rỗng (null)."));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(row.RowNumber))
                        {
                            errors.Add(new SeatingChartValidationError(
                                $"Khu vực '{sectionName}', Hàng thứ {rIdx + 1}",
                                $"sections[{sIdx}].rows[{rIdx}].rowNumber",
                                "MISSING_ROW_NUMBER",
                                $"Hàng ghế tại vị trí {rIdx + 1} thuộc khu vực '{sectionName}' không có mã hàng (rowNumber)."));
                        }
                        else
                        {
                            var rowKey = $"{sectionName}/{row.RowNumber.Trim()}";
                            if (!rowKeys.Add(rowKey))
                            {
                                errors.Add(new SeatingChartValidationError(
                                    $"Khu vực '{sectionName}', Hàng '{row.RowNumber}'",
                                    $"sections[{sIdx}].rows[{rIdx}].rowNumber",
                                    "DUPLICATE_ROW_NUMBER",
                                    $"Mã hàng '{row.RowNumber}' bị trùng lặp trong khu vực '{sectionName}'."));
                            }
                        }

                        var rowNumber = string.IsNullOrWhiteSpace(row.RowNumber) ? $"Hang_{rIdx + 1}" : row.RowNumber.Trim();
                        distinctRows.Add($"{sectionName}-{rowNumber}");

                        if (row.Seats == null || row.Seats.Count == 0)
                        {
                            errors.Add(new SeatingChartValidationError(
                                $"Khu vực '{sectionName}', Hàng '{rowNumber}'",
                                $"sections[{sIdx}].rows[{rIdx}].seats",
                                "EMPTY_ROW",
                                $"Hàng ghế '{rowNumber}' thuộc khu vực '{sectionName}' không có ghế nào."));
                            continue;
                        }

                        for (var seatIdx = 0; seatIdx < row.Seats.Count; seatIdx++)
                        {
                            var seat = row.Seats[seatIdx];
                            totalSeatsCounted++;
                            ValidateSeat(
                                seat,
                                sectionName,
                                rowNumber,
                                $"sections[{sIdx}].rows[{rIdx}].seats[{seatIdx}]",
                                seatIdx,
                                section.DefaultPrice,
                                section.Category,
                                errors,
                                seatIdentifiers,
                                coordinatesMap);
                        }
                    }
                }

                if (section.Seats != null)
                {
                    for (var seatIdx = 0; seatIdx < section.Seats.Count; seatIdx++)
                    {
                        var seat = section.Seats[seatIdx];
                        totalSeatsCounted++;

                        var rowName = seat?.Row;
                        if (string.IsNullOrWhiteSpace(rowName))
                        {
                            errors.Add(new SeatingChartValidationError(
                                $"Khu vực '{sectionName}', Ghế thứ {seatIdx + 1}",
                                $"sections[{sIdx}].seats[{seatIdx}].row",
                                "MISSING_ROW_NUMBER",
                                $"Mã hàng (row) là bắt buộc đối với ghế thứ {seatIdx + 1} trong khu vực '{sectionName}'."));
                            rowName = $"Hang_KhongXacDinh_{seatIdx + 1}";
                        }
                        else
                        {
                            distinctRows.Add($"{sectionName}-{rowName.Trim()}");
                        }

                        ValidateSeat(
                            seat,
                            sectionName,
                            rowName.Trim(),
                            $"sections[{sIdx}].seats[{seatIdx}]",
                            seatIdx,
                            section.DefaultPrice,
                            section.Category,
                            errors,
                            seatIdentifiers,
                            coordinatesMap);
                    }
                }
            }
        }

        if (dto.Seats != null)
        {
            for (var seatIdx = 0; seatIdx < dto.Seats.Count; seatIdx++)
            {
                var seat = dto.Seats[seatIdx];
                totalSeatsCounted++;

                var secName = seat?.Section;
                if (string.IsNullOrWhiteSpace(secName))
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Ghế thứ {seatIdx + 1}",
                        $"seats[{seatIdx}].section",
                        "MISSING_SECTION_NAME",
                        $"Tên khu vực (section) là bắt buộc đối với ghế thứ {seatIdx + 1}."));
                    secName = $"KhuVuc_KhongXacDinh_{seatIdx + 1}";
                }
                else
                {
                    distinctSections.Add(secName.Trim());
                }

                var rowName = seat?.Row;
                if (string.IsNullOrWhiteSpace(rowName))
                {
                    errors.Add(new SeatingChartValidationError(
                        $"Khu vực '{secName}', Ghế thứ {seatIdx + 1}",
                        $"seats[{seatIdx}].row",
                        "MISSING_ROW_NUMBER",
                        $"Mã hàng (row) là bắt buộc đối với ghế thứ {seatIdx + 1}."));
                    rowName = $"Hang_KhongXacDinh_{seatIdx + 1}";
                }
                else
                {
                    distinctRows.Add($"{secName.Trim()}-{rowName.Trim()}");
                }

                ValidateSeat(
                    seat,
                    secName.Trim(),
                    rowName.Trim(),
                    $"seats[{seatIdx}]",
                    seatIdx,
                    null,
                    null,
                    errors,
                    seatIdentifiers,
                    coordinatesMap);
            }
        }

        if (dto.TotalCapacity.HasValue)
        {
            if (dto.TotalCapacity.Value <= 0)
            {
                errors.Add(new SeatingChartValidationError(
                    "Tổng sức chứa (totalCapacity)",
                    "totalCapacity",
                    "INVALID_CAPACITY",
                    "Tổng sức chứa (totalCapacity) phải là số nguyên dương (> 0)."));
            }
            else if (dto.TotalCapacity.Value != totalSeatsCounted)
            {
                errors.Add(new SeatingChartValidationError(
                    "Tổng sức chứa (totalCapacity)",
                    "totalCapacity",
                    "CAPACITY_MISMATCH",
                    $"Tổng sức chứa khai báo ({dto.TotalCapacity.Value}) không khớp với tổng số lượng ghế trong sơ đồ ({totalSeatsCounted} ghế)."));
            }
        }

        if (errors.Count > 0)
        {
            return SeatingChartValidationResult.Failure(errors);
        }

        return SeatingChartValidationResult.Success(
            totalSeatsCounted,
            distinctSections.Count,
            distinctRows.Count,
            "Tệp sơ đồ hợp lệ.");
    }

    private static void ValidateSeat(
        SeatDto? seat,
        string sectionName,
        string rowName,
        string path,
        int seatIndex,
        decimal? defaultPrice,
        string? defaultCategory,
        List<SeatingChartValidationError> errors,
        Dictionary<string, string> seatIdentifiers,
        Dictionary<(double X, double Y), string> coordinatesMap)
    {
        if (seat == null)
        {
            errors.Add(new SeatingChartValidationError(
                $"Khu vực '{sectionName}', Hàng '{rowName}', Vị trí {seatIndex + 1}",
                path,
                "NULL_SEAT",
                $"Ghế tại vị trí {seatIndex + 1} thuộc hàng '{rowName}', khu vực '{sectionName}' có giá trị rỗng (null)."));
            return;
        }

        var seatNumber = seat.SeatNumber?.Trim();
        var location = $"Khu vực '{sectionName}', Hàng '{rowName}', Ghế '{(string.IsNullOrEmpty(seatNumber) ? $"[Vị trí {seatIndex + 1}]" : seatNumber)}'";

        if (string.IsNullOrWhiteSpace(seatNumber))
        {
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.seatNumber",
                "MISSING_SEAT_NUMBER",
                $"Số ghế (seatNumber) tại vị trí {seatIndex + 1} thuộc hàng '{rowName}', khu vực '{sectionName}' không được để trống."));
        }
        else
        {
            var identifierKey = $"{sectionName.ToUpperInvariant()}||{rowName.ToUpperInvariant()}||{seatNumber.ToUpperInvariant()}";
            if (seatIdentifiers.TryGetValue(identifierKey, out var firstSeenLocation))
            {
                errors.Add(new SeatingChartValidationError(
                    location,
                    $"{path}.seatNumber",
                    "DUPLICATE_SEAT",
                    $"Ghế '{seatNumber}' thuộc hàng '{rowName}', khu vực '{sectionName}' bị khai báo trùng lặp với ghế đã tồn tại tại {firstSeenLocation}."));
            }
            else
            {
                seatIdentifiers[identifierKey] = location;
            }
        }

        var effectivePrice = seat.Price ?? defaultPrice;
        if (!effectivePrice.HasValue)
        {
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.price",
                "MISSING_PRICE",
                $"Giá vé chưa được thiết lập cho ghế '{seatNumber ?? $"[vị trí {seatIndex + 1}]"}' (không có giá riêng và khu vực không có defaultPrice)."));
        }
        else if (effectivePrice.Value < 0)
        {
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.price",
                "NEGATIVE_PRICE",
                $"Giá vé ({effectivePrice.Value}) cho ghế '{seatNumber ?? $"[vị trí {seatIndex + 1}]"}' không được là số âm."));
        }

        if (!string.IsNullOrWhiteSpace(seat.Status) && !AllowedStatuses.Contains(seat.Status.Trim()))
        {
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.status",
                "INVALID_STATUS",
                $"Trạng thái ghế '{seat.Status}' không hợp lệ. Các trạng thái hợp lệ: {string.Join(", ", AllowedStatuses)}."));
        }

        var hasValidCoord = true;
        if (seat.X.HasValue && seat.X.Value < 0)
        {
            hasValidCoord = false;
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.x",
                "INVALID_COORDINATES",
                $"Tọa độ X ({seat.X.Value}) của ghế '{seatNumber ?? $"[vị trí {seatIndex + 1}]"}' không được là số âm."));
        }

        if (seat.Y.HasValue && seat.Y.Value < 0)
        {
            hasValidCoord = false;
            errors.Add(new SeatingChartValidationError(
                location,
                $"{path}.y",
                "INVALID_COORDINATES",
                $"Tọa độ Y ({seat.Y.Value}) của ghế '{seatNumber ?? $"[vị trí {seatIndex + 1}]"}' không được là số âm."));
        }

        if (seat.X.HasValue && seat.Y.HasValue && hasValidCoord)
        {
            var coordKey = (seat.X.Value, seat.Y.Value);
            var currentSeatDesc = $"Ghế '{(string.IsNullOrEmpty(seatNumber) ? $"[vị trí {seatIndex + 1}]" : seatNumber)}' ({sectionName}-{rowName})";

            if (coordinatesMap.TryGetValue(coordKey, out var existingSeatDesc))
            {
                errors.Add(new SeatingChartValidationError(
                    $"Tọa độ (X: {seat.X.Value}, Y: {seat.Y.Value})",
                    $"{path}.coordinates",
                    "DUPLICATE_COORDINATES",
                    $"Xung đột vị trí: {currentSeatDesc} có cùng tọa độ (X: {seat.X.Value}, Y: {seat.Y.Value}) với {existingSeatDesc}."));
            }
            else
            {
                coordinatesMap[coordKey] = currentSeatDesc;
            }
        }
    }
}

