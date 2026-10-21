using System.Globalization;
using System.Text;
using System.Text.Json;
using EventTicketing.Api.Models.Orders;
using EventTicketing.Api.Security;
using EventTicketing.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class PaymentsController(
    IPaymentService paymentService,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<PaymentsController>? logger = null,
    IPaymentGateway? paymentGateway = null) : ControllerBase
{
    private readonly IPaymentGateway _gateway = paymentGateway ?? new MockPaymentGateway();

    [HttpPost("orders/{id:int}/pay")]
    [Authorize(Policy = AccessPolicies.SeatBooking)]
    public async Task<IActionResult> PayOrder(int id, CancellationToken ct)
    {
        var userId = AccessPolicies.UserId(User);
        if (!userId.HasValue) return Unauthorized();

        var result = await paymentService.InitiatePaymentAsync(id, userId.Value, ct);

        return result.Outcome switch
        {
            InitiatePaymentOutcome.Ok => Ok(result.Data),
            InitiatePaymentOutcome.NotFound => NotFound(new
            {
                success = false,
                errorCode = "ORDER_NOT_FOUND",
                message = result.ErrorMessage
            }),
            InitiatePaymentOutcome.Expired => StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                errorCode = "ORDER_EXPIRED",
                message = result.ErrorMessage
            }),
            InitiatePaymentOutcome.NotPayable => StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                errorCode = "ORDER_NOT_PAYABLE",
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                success = false,
                message = result.ErrorMessage
            })
        };
    }

    [HttpPost("payments/sandbox/result")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleSandboxPaymentResult([FromBody] PaymentResultRequest request, CancellationToken ct)
    {
        var allowSandbox = environment.IsDevelopment() || configuration.GetValue<bool>("Payments:AllowAnonymousSandboxResult");
        if (!allowSandbox)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Cổng thanh toán sandbox chỉ khả dụng ở môi trường Development hoặc khi được bật cấu hình."
            });
        }

        var result = await paymentService.ConfirmPaymentAsync(request, ct);

        if (result.Outcome == ConfirmPaymentOutcome.OrderNotFound)
        {
            return NotFound(new
            {
                success = false,
                outcome = result.Outcome.ToString(),
                message = result.Message
            });
        }

        if (!request.Success || result.Outcome == ConfirmPaymentOutcome.GatewayFailed)
        {
            // Story S-24 (Task T-54): Ghi log nhật ký đơn hàng khi cổng thanh toán báo thất bại
            logger?.LogWarning(
                "Payment failed for order {OrderId}. TransactionId={TransactionId}, Amount={Amount}, Reason={Reason}, Timestamp={TimestampUtc}",
                request.OrderId,
                request.TransactionId,
                request.Amount,
                "PAYMENT_FAILED",
                DateTime.UtcNow);
        }

        var isSuccess = result.Outcome is ConfirmPaymentOutcome.Paid or ConfirmPaymentOutcome.AlreadyPaid;

        return Ok(new
        {
            success = isSuccess,
            outcome = result.Outcome.ToString(),
            message = result.Message
        });
    }

    /// <summary>
    /// S-19 / SCRUM-35 & S-20 / SCRUM-36: Webhook cho cổng thanh toán có kiểm tra chữ ký và chống xử lý trùng lặp (Idempotent).
    /// S-21 / SCRUM-37 (T-48, T-49): Kiểm chữ ký trước khi đọc nội dung; ghi log kèm IP khi từ chối.
    /// QA-B09: Signature Gate chạy TRƯỚC KHI parse body JSON để ngăn [ApiController] tự động trả 400 khi gặp malformed JSON.
    /// </summary>
    [HttpPost("payments/mock/webhook")]
    [HttpPost("payments/webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> HandleMockWebhook(
        [FromHeader(Name = "X-Webhook-Signature")] string? headerSignature = null,
        CancellationToken ct = default)
    {
        return await ProcessWebhookCoreAsync(request: null, headerSignature, ct);
    }

    /// <summary>
    /// Direct in-memory overload for backward compatibility with existing unit tests.
    /// </summary>
    [NonAction]
    public async Task<IActionResult> HandleMockWebhook(
        MockWebhookRequest request,
        string? headerSignature = null,
        CancellationToken ct = default)
    {
        return await ProcessWebhookCoreAsync(request, headerSignature, ct);
    }

    private async Task<IActionResult> ProcessWebhookCoreAsync(
        MockWebhookRequest? request,
        string? headerSignature,
        CancellationToken ct)
    {
        var remoteIp = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        // If request DTO is explicitly supplied (e.g. from existing unit tests or in-memory calls)
        if (request != null)
        {
            var signature = request.Signature ?? headerSignature ?? HttpContext?.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var payload = $"{request.OrderId}|{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.TransactionId}|{request.Success.ToString().ToLowerInvariant()}";

            if (!_gateway.VerifyWebhookSignature(payload, signature ?? string.Empty))
            {
                logger?.LogWarning(
                    "Webhook signature rejected. RemoteIP={RemoteIP}, StatusCode={StatusCode}, Reason={Reason}, Timestamp={TimestampUtc}",
                    remoteIp,
                    401,
                    "INVALID_SIGNATURE",
                    DateTime.UtcNow);

                return StatusCode(StatusCodes.Status401Unauthorized, new
                {
                    success = false,
                    errorCode = "INVALID_SIGNATURE",
                    message = "Chữ ký webhook không hợp lệ."
                });
            }

            return await ProcessValidatedWebhookAsync(request, payload, signature, remoteIp, ct);
        }

        // --- HTTP Pipeline processing (no [FromBody] model binding to prevent 400 before 401) ---
        // 1. Check body size limit
        if (HttpContext?.Request.ContentLength > 65_536)
        {
            logger?.LogWarning(
                "Webhook payload size exceeds limit. RemoteIP={RemoteIP}, StatusCode={StatusCode}, Reason={Reason}, Timestamp={TimestampUtc}",
                remoteIp,
                413,
                "PAYLOAD_TOO_LARGE",
                DateTime.UtcNow);

            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                success = false,
                errorCode = "PAYLOAD_TOO_LARGE",
                message = "Dung lượng webhook vượt quá giới hạn 64KB."
            });
        }

        // 2. Signature Gate FIRST: Check signature from header
        var effectiveSignature = headerSignature;
        if (string.IsNullOrWhiteSpace(effectiveSignature) && HttpContext != null)
        {
            if (HttpContext.Request.Headers.TryGetValue("X-Webhook-Signature", out var headerVal))
            {
                effectiveSignature = headerVal.FirstOrDefault();
            }
        }

        if (string.IsNullOrWhiteSpace(effectiveSignature))
        {
            logger?.LogWarning(
                "Webhook signature rejected. RemoteIP={RemoteIP}, StatusCode={StatusCode}, Reason={Reason}, Timestamp={TimestampUtc}",
                remoteIp,
                401,
                "INVALID_SIGNATURE",
                DateTime.UtcNow);

            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                success = false,
                errorCode = "INVALID_SIGNATURE",
                message = "Chữ ký webhook không hợp lệ."
            });
        }

        // 3. Read raw body bytes / string directly from Request.Body (UTF-8 no BOM, unnormalized)
        string rawBody;
        if (HttpContext?.Request.Body != null)
        {
            if (HttpContext.Request.Body.CanSeek)
            {
                HttpContext.Request.Body.Position = 0;
            }
            using var reader = new StreamReader(
                HttpContext.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            rawBody = await reader.ReadToEndAsync(ct);
        }
        else
        {
            rawBody = string.Empty;
        }

        // 4. Signature Gate: Verify signature against raw HTTP body bytes using constant-time comparison
        if (!_gateway.VerifyWebhookSignature(rawBody, effectiveSignature))
        {
            logger?.LogWarning(
                "Webhook signature rejected. RemoteIP={RemoteIP}, StatusCode={StatusCode}, Reason={Reason}, Timestamp={TimestampUtc}",
                remoteIp,
                401,
                "INVALID_SIGNATURE",
                DateTime.UtcNow);

            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                success = false,
                errorCode = "INVALID_SIGNATURE",
                message = "Chữ ký webhook không hợp lệ."
            });
        }

        // 5. Parse JSON AFTER Signature Gate PASSES
        MockWebhookRequest? webhookData;
        try
        {
            webhookData = JsonSerializer.Deserialize<MockWebhookRequest>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            logger?.LogWarning(
                "Webhook malformed JSON payload after signature check. RemoteIP={RemoteIP}, StatusCode={StatusCode}, Reason={Reason}, Timestamp={TimestampUtc}",
                remoteIp,
                400,
                "MALFORMED_JSON",
                DateTime.UtcNow);

            return BadRequest(new
            {
                success = false,
                errorCode = "MALFORMED_JSON",
                message = "Nội dung webhook không phải JSON hợp lệ."
            });
        }

        if (webhookData == null || webhookData.OrderId <= 0 || string.IsNullOrWhiteSpace(webhookData.TransactionId))
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "INVALID_DATA",
                message = "Dữ liệu webhook thiếu thông tin bắt buộc."
            });
        }

        return await ProcessValidatedWebhookAsync(webhookData, rawBody, effectiveSignature, remoteIp, ct);
    }

    private async Task<IActionResult> ProcessValidatedWebhookAsync(
        MockWebhookRequest webhookData,
        string rawPayload,
        string? signature,
        string remoteIp,
        CancellationToken ct)
    {
        var webhookRequest = new WebhookPaymentRequest(
            OrderId: webhookData.OrderId,
            TransactionId: webhookData.TransactionId ?? string.Empty,
            Amount: webhookData.Amount,
            Success: webhookData.Success,
            RawPayload: rawPayload,
            Signature: signature);

        var result = await paymentService.ProcessWebhookAsync(webhookRequest, ct);

        if (result.Outcome == ProcessWebhookOutcome.OrderNotFound)
        {
            // T-49: Chữ ký đúng nhưng đơn hàng không tồn tại – log kèm mã đơn và IP.
            logger?.LogWarning(
                "Webhook order not found. RemoteIP={RemoteIP}, StatusCode={StatusCode}, OrderId={OrderId}, Reason={Reason}, Timestamp={TimestampUtc}",
                remoteIp,
                404,
                webhookData.OrderId,
                "ORDER_NOT_FOUND",
                DateTime.UtcNow);

            return NotFound(new
            {
                success = false,
                outcome = result.Outcome.ToString(),
                message = result.Message
            });
        }

        if (result.Outcome == ProcessWebhookOutcome.InvalidData)
        {
            return BadRequest(new
            {
                success = false,
                outcome = result.Outcome.ToString(),
                message = result.Message
            });
        }

        if (!webhookData.Success || result.PaymentResult?.Outcome == ConfirmPaymentOutcome.GatewayFailed)
        {
            // Story S-24 (Task T-54): Thất bại tại cổng thanh toán - Ghi log nhật ký đơn hàng thanh toán thất bại
            logger?.LogWarning(
                "Payment failed for order {OrderId}. TransactionId={TransactionId}, Amount={Amount}, Reason={Reason}, Timestamp={TimestampUtc}",
                webhookData.OrderId,
                webhookData.TransactionId,
                webhookData.Amount,
                "PAYMENT_FAILED",
                DateTime.UtcNow);
        }

        // Bỏ qua mã đã có, trả 200 OK cho webhook trùng để cổng ngừng gửi lại; không có lỗi 500
        return Ok(new
        {
            success = result.Success,
            isDuplicate = result.IsDuplicate,
            outcome = result.Outcome == ProcessWebhookOutcome.DuplicateIgnored
                ? "DuplicateIgnored"
                : result.PaymentResult?.Outcome.ToString(),
            message = result.Message
        });
    }

    /// <summary>
    /// S-19 / SCRUM-35: Ký webhook giả lập để mô phỏng chữ ký hợp lệ từ cổng thanh toán
    /// </summary>
    [HttpPost("payments/mock/sign")]
    [AllowAnonymous]
    public IActionResult GenerateMockSignature([FromBody] MockSignRequest request)
    {
        var payload = $"{request.OrderId}|{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.TransactionId}|{request.Success.ToString().ToLowerInvariant()}";
        var signature = _gateway.GenerateWebhookSignature(payload);
        return Ok(new MockSignResponse(signature, payload));
    }
}
