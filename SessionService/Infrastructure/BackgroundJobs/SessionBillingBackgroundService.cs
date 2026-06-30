using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionService.Domain.Entities;
using SessionService.Infrastructure.Data;

namespace SessionService.Infrastructure.BackgroundJobs;

/// <summary>
/// Mỗi giây, với từng session đang Active:
///   - Tính tiền phải trừ cho giây vừa trôi qua (PricePerHour/3600 * (1-Discount))
///   - Gọi WalletService trừ tiền
///   - Nếu trừ thất bại (hết tiền) -> tự động đóng phiên + gọi MachineService release máy
/// </summary>
public class SessionBillingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SessionBillingBackgroundService> _logger;

    public SessionBillingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SessionBillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ 1 giây để app khởi động xong hẳn
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessActiveSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý billing cho active sessions");
            }
        }
    }

    private async Task ProcessActiveSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SessionDbContext>();

        var activeSessions = await context.Sessions
            .Where(s => s.Status == SessionStatus.Active)
            .ToListAsync(ct);

        if (activeSessions.Count == 0) return;

        var walletClient = _httpClientFactory.CreateClient("WalletService");
        var machineClient = _httpClientFactory.CreateClient("MachineService");

        foreach (var session in activeSessions)
        {
            // Tiền phải trừ cho 1 giây = (giá/giờ / 3600) * (1 - discount)
            var amountPerSecond = (session.PricePerHour / 3600m) * (1 - session.Discount);
            // Làm tròn 2 chữ số thập phân để không lệch khi cộng dồn nhiều lần
            amountPerSecond = Math.Round(amountPerSecond, 2);

            if (amountPerSecond <= 0) continue;

            var success = await TryDeductWalletAsync(walletClient, session.UserId, amountPerSecond, session.Id, ct);

            if (!success)
            {
                // Hết tiền -> tự động đóng phiên
                _logger.LogWarning("User {UserId} hết tiền, tự động đóng session {SessionId}", session.UserId, session.Id);

                session.Close();
                await context.SaveChangesAsync(ct);

                // Gọi MachineService giải phóng máy
                await TryReleaseMachineAsync(machineClient, session.MachineId, ct);
            }
            else
            {
                // Cộng dồn tiền đã trừ vào session để khi đóng phiên có TotalCost đúng
                session.AccumulateCost(amountPerSecond);
                await context.SaveChangesAsync(ct);
            }
        }
    }

    private async Task<bool> TryDeductWalletAsync(HttpClient client, Guid userId, decimal amount, Guid sessionId, CancellationToken ct)
    {
        try
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                userId,
                amount,
                note = $"Phí chơi máy - phiên {sessionId}"
            });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/wallet/deduct", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Lỗi kết nối WalletService -> coi như fail, không trừ được
            return false;
        }
    }

    private async Task TryReleaseMachineAsync(HttpClient client, Guid machineId, CancellationToken ct)
    {
        try
        {
            await client.PostAsync($"/api/machines/{machineId}/release", null, ct);
        }
        catch (Exception)
        {
            // Bỏ qua lỗi, không làm crash background job
        }
    }
}
