using EventTicketing.Api.Entities;

namespace EventTicketing.Api.Services;

public enum HoldFailureReason
{
    AlreadyHeld,
    AlreadySold,
    SalesClosed,
    NotFound
}

public sealed record HeldSeatDto(int Id, string Row, string Number, int SeatCategoryId, DateTime HeldUntilUtc);

public sealed record SeatDisplayDto(
    int Id,
    int PerformanceId,
    int SeatCategoryId,
    string Row,
    string Number,
    string DisplayStatus,
    DateTime? HeldUntilUtc,
    bool CanSelect);

public sealed record UserHoldSessionDto(
    int PerformanceId,
    int UserId,
    DateTime HeldUntilUtc,
    DateTime ServerTimeUtc,
    int RemainingSeconds,
    IReadOnlyList<HeldSeatDto> HeldSeats);

public sealed class HoldSeatResult
{
    public bool IsSuccess { get; private init; }
    public int? SeatId { get; private init; }
    public SeatStatus? Status { get; private init; }
    public DateTime? HeldUntilUtc { get; private init; }
    public DateTime ServerTimeUtc { get; private init; }
    public int RemainingSeconds { get; private init; }
    public IReadOnlyList<HeldSeatDto> HeldSeats { get; private init; } = Array.Empty<HeldSeatDto>();
    public string? ErrorMessage { get; private init; }
    public HoldFailureReason? FailureReason { get; private init; }
    public Seat? CurrentSeat { get; private init; }

    public static HoldSeatResult Success(
        int seatId,
        DateTime heldUntilUtc,
        DateTime serverTimeUtc,
        int remainingSeconds,
        IReadOnlyList<HeldSeatDto> heldSeats) =>
        new()
        {
            IsSuccess = true,
            SeatId = seatId,
            Status = SeatStatus.Held,
            HeldUntilUtc = heldUntilUtc,
            ServerTimeUtc = serverTimeUtc,
            RemainingSeconds = remainingSeconds,
            HeldSeats = heldSeats
        };

    public static HoldSeatResult Failed(
        string errorMessage,
        HoldFailureReason reason,
        DateTime serverTimeUtc,
        Seat? currentSeat = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            FailureReason = reason,
            ServerTimeUtc = serverTimeUtc,
            CurrentSeat = currentSeat
        };
}

public interface ISeatReservationService
{
    Task<HoldSeatResult> HoldSeatAsync(
        int performanceId,
        int seatId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseSeatAsync(
        int performanceId,
        int seatId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeatDisplayDto>> GetSeatingChartStatusAsync(
        int performanceId,
        int? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<UserHoldSessionDto?> GetUserHoldSessionAsync(
        int performanceId,
        int userId,
        CancellationToken cancellationToken = default);
}

