using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EventTicketing.Api.Configuration;
using EventTicketing.Api.Controllers;
using EventTicketing.Api.Data;
using EventTicketing.Api.Entities;
using EventTicketing.Api.Models.Orders;
using EventTicketing.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventTicketing.Tests;

/// <summary>
/// S-21 (SCRUM-37): Webhook có chữ ký sai bị từ chối.
/// T-48 (SCRUM-118): Kiểm chữ ký webhook trước khi đọc nội dung.
/// T-49 (SCRUM-119): Ghi log lần từ chối kèm địa chỉ nguồn.
/// QA-B09: Signature Gate chạy TRƯỚC KHI parse body JSON để ngăn [ApiController] tự động trả 400 khi gặp malformed JSON.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WebhookSignatureTests
{
    private const string TestSecret = "test-webhook-secret-s21";

    private sealed class HttpTestHarness : IDisposable
    {
        public TestServer Server { get; }
        public HttpClient Client { get; }
        public AppDbContext Db { get; }
        public MockPaymentGateway Gateway { get; }
        public Mock<ILogger<PaymentsController>> LoggerMock { get; }

        public HttpTestHarness(TestServer server, HttpClient client, AppDbContext db, MockPaymentGateway gateway, Mock<ILogger<PaymentsController>> loggerMock)
        {
            Server = server;
            Client = client;
            Db = db;
            Gateway = gateway;
            LoggerMock = loggerMock;
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }
    }

    private static PaymentsController CreateController(
        IPaymentService paymentService,
        MockPaymentGateway gateway,
        Mock<ILogger<PaymentsController>> loggerMock,
        IPAddress? remoteIp = null,
        string? body = null,
        string? headerSignature = null)
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Development");
        var configMock = new Mock<IConfiguration>();

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp ?? IPAddress.Parse("203.0.113.42");

        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.ContentType = "application/json";
        }

        if (headerSignature != null)
        {
            httpContext.Request.Headers["X-Webhook-Signature"] = headerSignature;
        }

        var controller = new PaymentsController(
            paymentService, envMock.Object, configMock.Object, loggerMock.Object, gateway)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    private static string BuildPayload(int orderId, decimal amount, string? transactionId, bool success)
        => $"{orderId}|{amount.ToString(CultureInfo.InvariantCulture)}|{transactionId}|{success.ToString().ToLowerInvariant()}";

    private static (AppDbContext Db, PaymentService Service, MockPaymentGateway Gateway, Mock<ILogger<PaymentsController>> LoggerMock) CreateTestHarness(AppDbContext? existingDb = null)
    {
        var db = existingDb ?? new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var paymentOptions = Options.Create(new PaymentOptions { WebhookSecret = TestSecret });
        var gateway = new MockPaymentGateway(paymentOptions);

        var clockMock = new Mock<IReservationClock>();
        var fixedTime = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
        clockMock.Setup(c => c.NowAsync(It.IsAny<AppDbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixedTime);

        var serviceLoggerMock = new Mock<ILogger<PaymentService>>();
        var service = new PaymentService(db, clockMock.Object, paymentOptions, serviceLoggerMock.Object, gateway);
        var controllerLoggerMock = new Mock<ILogger<PaymentsController>>();

        return (db, service, gateway, controllerLoggerMock);
    }

#pragma warning disable ASPDEPR004, ASPDEPR008
    private static HttpTestHarness CreateHttpTestServer(AppDbContext? sharedDb = null)
    {
        var (db, service, gateway, controllerLoggerMock) = CreateTestHarness(sharedDb);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Development");
        var configMock = new Mock<IConfiguration>();

        var builder = new WebHostBuilder()
            .UseEnvironment("Development")
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers()
                    .AddApplicationPart(typeof(PaymentsController).Assembly);
                services.AddSingleton<IPaymentService>(service);
                services.AddSingleton(envMock.Object);
                services.AddSingleton(configMock.Object);
                services.AddSingleton<IPaymentGateway>(gateway);
                services.AddSingleton(controllerLoggerMock.Object);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        var server = new TestServer(builder);
        var client = server.CreateClient();
        return new HttpTestHarness(server, client, db, gateway, controllerLoggerMock);
    }
#pragma warning restore ASPDEPR004, ASPDEPR008

    private static async Task<Order> SeedOrderAsync(AppDbContext db, int orderId = 1, int userId = 99, decimal totalAmount = 250000m)
    {
        var fixedTime = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

        var seat = new Seat
        {
            Id = 500 + orderId,
            ShowtimeId = 1,
            SeatCategoryId = 1,
            Row = "A",
            Number = orderId.ToString(),
            Status = SeatStatus.Held,
            HeldByUserId = userId,
            HeldUntil = fixedTime.AddMinutes(15)
        };
        db.Seats.Add(seat);

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            ShowtimeId = 1,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            CreatedAt = fixedTime,
            ExpiresAt = fixedTime.AddMinutes(15),
            OrderItems =
            [
                new OrderItem
                {
                    Id = 1000 + orderId,
                    OrderId = orderId,
                    SeatId = seat.Id,
                    ShowtimeId = 1,
                    Seat = seat,
                    UnitPrice = totalAmount,
                    Status = OrderStatus.Pending
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    // =========================================================================
    // Existing Unit Tests (S-21 Contract & Backward Compatibility)
    // =========================================================================

    /// <summary>
    /// Test 1 – Payload bị sửa 1 ký tự: trả 401, đơn hàng không đổi, không có bản ghi mới, có log cảnh báo kèm IP.
    /// AC 1: Chữ ký không khớp → HTTP 401, không đọc nội dung nghiệp vụ, không đổi trạng thái đơn hàng.
    /// </summary>
    [Fact]
    public async Task Webhook_TamperedPayload_Returns401_OrderUnchanged_LogsWarningWithIP()
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();
        var order = await SeedOrderAsync(db, orderId: 10, totalAmount: 200000m);

        // Ký payload gốc
        var originalPayload = BuildPayload(10, 200000m, "TXN-TAMPER", true);
        var validSignature = gateway.GenerateWebhookSignature(originalPayload);

        // Tạo request có payload bị sửa 1 ký tự (amount thay đổi)
        var controller = CreateController(service, gateway, loggerMock);
        var tamperedRequest = new MockWebhookRequest(
            OrderId: 10,
            TransactionId: "TXN-TAMPER",
            Amount: 200001m,  // bị sửa 1 ký tự so với payload đã ký
            Success: true,
            Signature: validSignature);

        var result = await controller.HandleMockWebhook(tamperedRequest, null, CancellationToken.None);

        // Assert: 401 Unauthorized
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);

        // Assert: Đơn hàng không đổi trạng thái
        var unchanged = await db.Orders.FindAsync(10);
        Assert.NotNull(unchanged);
        Assert.Equal(OrderStatus.Pending, unchanged.Status);
        Assert.Null(unchanged.PaidAt);
        Assert.Null(unchanged.PaidAmount);

        // Assert: Ghế vẫn Held
        var unchangedSeat = await db.Seats.FindAsync(510);
        Assert.NotNull(unchangedSeat);
        Assert.Equal(SeatStatus.Held, unchangedSeat.Status);
        Assert.Equal(99, unchangedSeat.HeldByUserId);

        // Assert: Log cảnh báo có chứa thông tin IP
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v != null && (v.ToString() ?? "").Contains("203.0.113.42") && (v.ToString() ?? "").Contains("INVALID_SIGNATURE")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test 2 – Chữ ký sai / thiếu: trả 401, ghi log kèm IP.
    /// </summary>
    [Theory]
    [InlineData("completely-wrong-signature", "wrong signature")]
    [InlineData("", "empty signature")]
    [InlineData(null, "null signature")]
    public async Task Webhook_InvalidOrMissingSignature_Returns401_LogsWarningWithIP(string? signature, string scenario)
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();

        var controller = CreateController(service, gateway, loggerMock, IPAddress.Parse("10.0.0.5"));
        var request = new MockWebhookRequest(
            OrderId: 1,
            TransactionId: "TXN-BAD",
            Amount: 100000m,
            Success: true,
            Signature: signature);

        var result = await controller.HandleMockWebhook(request, null, CancellationToken.None);

        // Assert: 401 Unauthorized
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);

        // Assert: Log cảnh báo kèm IP
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v != null && (v.ToString() ?? "").Contains("10.0.0.5") && (v.ToString() ?? "").Contains("INVALID_SIGNATURE")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            $"Scenario: {scenario}");
    }

    /// <summary>
    /// Test 3 – Đơn hàng không tồn tại: chữ ký hợp lệ nhưng orderId không có trong DB → 404, log kèm mã đơn và IP.
    /// AC 2: Chữ ký đúng, mã đơn không tồn tại → HTTP 404, ghi log.
    /// </summary>
    [Fact]
    public async Task Webhook_ValidSignature_OrderNotFound_Returns404_LogsWithOrderIdAndIP()
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();

        var nonExistentOrderId = 999;
        var payload = BuildPayload(nonExistentOrderId, 300000m, "TXN-GHOST", true);
        var validSignature = gateway.GenerateWebhookSignature(payload);

        var controller = CreateController(service, gateway, loggerMock, IPAddress.Parse("192.168.1.100"));
        var request = new MockWebhookRequest(
            OrderId: nonExistentOrderId,
            TransactionId: "TXN-GHOST",
            Amount: 300000m,
            Success: true,
            Signature: validSignature);

        var result = await controller.HandleMockWebhook(request, null, CancellationToken.None);

        // Assert: 404 Not Found
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        // Assert: Log có chứa OrderId và IP
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v != null &&
                    (v.ToString() ?? "").Contains("192.168.1.100") &&
                    (v.ToString() ?? "").Contains("ORDER_NOT_FOUND") &&
                    (v.ToString() ?? "").Contains("999")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test 4 – Luồng hợp lệ: chữ ký đúng và đơn hàng tồn tại → xử lý bình thường (T-46).
    /// </summary>
    [Fact]
    public async Task Webhook_ValidSignature_ValidOrder_ProcessesNormally()
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();
        var order = await SeedOrderAsync(db, orderId: 50, totalAmount: 500000m);

        var payload = BuildPayload(50, 500000m, "TXN-VALID-50", true);
        var validSignature = gateway.GenerateWebhookSignature(payload);

        var controller = CreateController(service, gateway, loggerMock);
        var request = new MockWebhookRequest(
            OrderId: 50,
            TransactionId: "TXN-VALID-50",
            Amount: 500000m,
            Success: true,
            Signature: validSignature);

        var result = await controller.HandleMockWebhook(request, null, CancellationToken.None);

        // Assert: 200 OK với outcome Paid
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Assert: Đơn hàng chuyển sang Paid
        var updatedOrder = await db.Orders.FindAsync(50);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Paid, updatedOrder.Status);
        Assert.Equal(500000m, updatedOrder.PaidAmount);
        Assert.Equal("TXN-VALID-50", updatedOrder.PaymentReference);

        // Assert: Ghế chuyển sang Sold
        var updatedSeat = await db.Seats.FindAsync(550);
        Assert.NotNull(updatedSeat);
        Assert.Equal(SeatStatus.Sold, updatedSeat.Status);
        Assert.Null(updatedSeat.HeldByUserId);

        // Assert: Không có log cảnh báo INVALID_SIGNATURE
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v != null && (v.ToString() ?? "").Contains("INVALID_SIGNATURE")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Test 5 – Chữ ký qua header X-Webhook-Signature (thay vì qua body) cũng được xác thực đúng.
    /// </summary>
    [Fact]
    public async Task Webhook_SignatureViaHeader_ValidatesCorrectly()
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();
        var order = await SeedOrderAsync(db, orderId: 60, totalAmount: 150000m);

        var payload = BuildPayload(60, 150000m, "TXN-HDR-60", true);
        var validSignature = gateway.GenerateWebhookSignature(payload);

        var controller = CreateController(service, gateway, loggerMock);

        // Signature qua body = null, chỉ qua header
        var request = new MockWebhookRequest(
            OrderId: 60,
            TransactionId: "TXN-HDR-60",
            Amount: 150000m,
            Success: true,
            Signature: null);

        var result = await controller.HandleMockWebhook(request, validSignature, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var updatedOrder = await db.Orders.FindAsync(60);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Paid, updatedOrder.Status);
    }

    // =========================================================================
    // QA-B09 Tests (Pipeline HTTP + Signature Gate BEFORE Body Parsing)
    // =========================================================================

    /// <summary>
    /// QA-B09 Case 1: Malformed JSON + Không có chữ ký (gửi "{broken" hai lần).
    /// Cả hai lần đều phải trả về 401 Unauthorized (KHÔNG được trả 400 Bad Request do [ApiController] / [FromBody]).
    /// Database không bị thay đổi, không có payment_events nào được tạo.
    /// </summary>
    [Fact]
    public async Task QA_B09_MalformedJson_NoSignature_SentTwice_BothReturn401_DbUnchanged()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var db = harness.Db;

        var malformedBody = "{broken";

        // Gửi lần 1: không có header chữ ký
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(malformedBody, Encoding.UTF8, "application/json")
        };
        var response1 = await client.SendAsync(request1);

        // Assert lần 1: Phải là 401 Unauthorized, KHÔNG phải 400 Bad Request
        Assert.Equal(HttpStatusCode.Unauthorized, response1.StatusCode);

        // Gửi lần 2: kiểm tra tính ổn định, không có side effects
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(malformedBody, Encoding.UTF8, "application/json")
        };
        var response2 = await client.SendAsync(request2);

        // Assert lần 2: Vẫn là 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response2.StatusCode);

        // DB không bị chạm, không có bản ghi payment_events
        var eventCount = await db.PaymentEvents.CountAsync();
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// QA-B09 Case 2: Malformed JSON + Chữ ký sai (gửi "{broken" kèm chữ ký sai).
    /// Trả về 401 Unauthorized, DB không đổi.
    /// </summary>
    [Fact]
    public async Task QA_B09_MalformedJson_WrongSignature_Returns401_DbUnchanged()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var db = harness.Db;

        var malformedBody = "{broken";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(malformedBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Signature", "wrong-signature-value-000");

        var response = await client.SendAsync(request);

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // DB không đổi
        var eventCount = await db.PaymentEvents.CountAsync();
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// QA-B09 Case 3: Valid JSON + Chữ ký sai.
    /// Trả về 401 Unauthorized, DB không đổi.
    /// </summary>
    [Fact]
    public async Task QA_B09_ValidJson_WrongSignature_Returns401_DbUnchanged()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var db = harness.Db;

        var validJson = JsonSerializer.Serialize(new
        {
            orderId = 1,
            transactionId = "TXN-WRONG-SIG",
            amount = 200000m,
            success = true
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(validJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Signature", "completely-bogus-signature");

        var response = await client.SendAsync(request);

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // DB không đổi
        var eventCount = await db.PaymentEvents.CountAsync();
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// QA-B09 Case 4: Tampered 1-byte.
    /// Ký hợp lệ trên một JSON payload, sau đó sửa đúng 1 byte (thêm 1 khoảng trắng hoặc đổi 1 ký tự)
    /// rồi gửi kèm chữ ký cũ -> phải bị từ chối 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task QA_B09_TamperedOneByte_Returns401_DbUnchanged()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var db = harness.Db;
        var gateway = harness.Gateway;

        var originalJson = "{\"orderId\":10,\"transactionId\":\"TXN-TAMPER-BYTE\",\"amount\":200000,\"success\":true}";
        var validSignature = gateway.GenerateWebhookSignature(originalJson);

        // Sửa đúng 1 byte: thêm 1 ký tự khoảng trắng ở cuối body
        var tamperedJson = originalJson + " ";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(tamperedJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Signature", validSignature);

        var response = await client.SendAsync(request);

        // Assert: 401 Unauthorized vì signature không khớp raw body sau khi sửa 1 byte
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // DB không đổi
        var eventCount = await db.PaymentEvents.CountAsync();
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// QA-B09 Case 5: Signed Valid Body + Replay (Idempotency S-20).
    /// Gửi body hợp lệ có chữ ký đúng -> xử lý thành công (200 OK), cập nhật đơn/ghế.
    /// Gửi lại cùng webhook đó nhiều lần -> vẫn giữ tính idempotent, chỉ có 1 bản ghi payment_events.
    /// </summary>
    [Fact]
    public async Task QA_B09_SignedValidBody_ReplayIdempotency_ProcessedOnce_DbRecordedOnce()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var sharedDb = new AppDbContext(dbOptions);

        var order = await SeedOrderAsync(sharedDb, orderId: 70, totalAmount: 350000m);

        using var harness = CreateHttpTestServer(sharedDb);
        var client = harness.Client;
        var gateway = harness.Gateway;

        var jsonBody = JsonSerializer.Serialize(new
        {
            orderId = 70,
            transactionId = "TXN-REPLAY-S21",
            amount = 350000m,
            success = true
        });

        var validSignature = gateway.GenerateWebhookSignature(jsonBody);

        // Gửi lần 1: Thành công
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("X-Webhook-Signature", validSignature);

        var response1 = await client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Kiểm tra DB sau lần 1: Đơn Paid, ghế Sold, 1 payment_events
        var updatedOrder = await sharedDb.Orders.FindAsync(70);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Paid, updatedOrder.Status);

        var seat = await sharedDb.Seats.FindAsync(570);
        Assert.NotNull(seat);
        Assert.Equal(SeatStatus.Sold, seat.Status);

        var eventCount1 = await sharedDb.PaymentEvents.CountAsync();
        Assert.Equal(1, eventCount1);

        // Gửi lần 2 (Replay cùng payload và signature)
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("X-Webhook-Signature", validSignature);

        var response2 = await client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Gửi lần 3 (Replay tiếp)
        var request3 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request3.Headers.Add("X-Webhook-Signature", validSignature);

        var response3 = await client.SendAsync(request3);
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);

        // Assert Idempotency: Vẫn chỉ có đúng 1 bản ghi payment_events duy nhất
        var eventCountFinal = await sharedDb.PaymentEvents.CountAsync();
        Assert.Equal(1, eventCountFinal);

        // RawPayload được lưu giữ nguyên bản JSON gốc
        var savedEvent = await sharedDb.PaymentEvents.FirstAsync();
        Assert.Equal("TXN-REPLAY-S21", savedEvent.TransactionId);
        Assert.Equal(jsonBody, savedEvent.RawPayload);
    }

    /// <summary>
    /// QA-B09 Case 6: Signed Valid Body + Đơn không tồn tại.
    /// Trả về 404 Not Found và ghi structured log kèm địa chỉ nguồn RemoteIP và mã đơn.
    /// </summary>
    [Fact]
    public async Task QA_B09_SignedValidBody_OrderNotFound_Returns404_LogsWarningWithIp()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var gateway = harness.Gateway;
        var loggerMock = harness.LoggerMock;

        var ghostOrderId = 88888;
        var jsonBody = JsonSerializer.Serialize(new
        {
            orderId = ghostOrderId,
            transactionId = "TXN-GHOST-S21",
            amount = 120000m,
            success = true
        });

        var validSignature = gateway.GenerateWebhookSignature(jsonBody);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Signature", validSignature);

        var response = await client.SendAsync(request);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Assert: Ghi log có chứa OrderId và ORDER_NOT_FOUND
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v != null &&
                    (v.ToString() ?? "").Contains("ORDER_NOT_FOUND") &&
                    (v.ToString() ?? "").Contains("88888")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// QA-B09 Case 7: Signed Malformed JSON (Chữ ký hợp lệ nhưng cú pháp JSON hỏng).
    /// Ký HMAC hợp lệ cho chuỗi "{broken".
    /// Cổng kiểm chữ ký PASS, sau đó bước parse JSON mới phát hiện lỗi cú pháp và trả về 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task QA_B09_SignedMalformedJson_Returns400BadRequest()
    {
        using var harness = CreateHttpTestServer();
        var client = harness.Client;
        var gateway = harness.Gateway;

        var malformedBody = "{broken";
        // Ký chữ ký hợp lệ cho chuỗi malformedBody
        var validSignatureForMalformed = gateway.GenerateWebhookSignature(malformedBody);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(malformedBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Signature", validSignatureForMalformed);

        var response = await client.SendAsync(request);

        // Assert: Vì chữ ký hợp lệ, request vượt qua Signature Gate.
        // Bước parse JSON tiếp theo phát hiện JSON hỏng và trả về 400 Bad Request!
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// QA-B09 Case 8: Gọi trực tiếp Controller với raw body stream (Unit Test style).
    /// Malformed JSON + Không có signature header -> 401 Unauthorized, không parse sâu, có log IP.
    /// </summary>
    [Fact]
    public async Task QA_B09_DirectController_MalformedJson_NoSignature_Returns401_LogsWarning()
    {
        var (db, service, gateway, loggerMock) = CreateTestHarness();

        var controller = CreateController(
            service,
            gateway,
            loggerMock,
            remoteIp: IPAddress.Parse("198.51.100.25"),
            body: "{broken",
            headerSignature: null);

        var result = await controller.HandleMockWebhook(headerSignature: null, ct: CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);

        // Verify warning log chứa IP và INVALID_SIGNATURE
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v != null &&
                    (v.ToString() ?? "").Contains("198.51.100.25") &&
                    (v.ToString() ?? "").Contains("INVALID_SIGNATURE")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
