using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionService.Domain.Entities;
using SessionService.Infrastructure.Data;

namespace SessionService.Infrastructure.BackgroundJobs;

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
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        _logger.LogInformation("SessionBillingBackgroundService started - billing every 1 minute");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Billing error"); }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SessionDbContext>();

        var sessions = await context.Sessions
            .Where(s => s.Status == SessionStatus.Active)
            .ToListAsync(ct);

        if (!sessions.Any())
        {
            _logger.LogInformation("No active sessions to bill");
            return;
        }

        _logger.LogInformation("Billing {Count} active sessions", sessions.Count);

        var walletClient = _httpClientFactory.CreateClient("WalletService");
        var machineClient = _httpClientFactory.CreateClient("MachineService");

        foreach (var session in sessions)
        {
            // Tiền mỗi phút = PricePerHour / 60 * (1 - Discount), làm tròn
            var amountPerMinute = Math.Round((session.PricePerHour / 60m) * (1 - session.Discount), 0);
            if (amountPerMinute <= 0) continue;

            _logger.LogInformation("Billing session {SessionId}: {Amount}đ/min, userId={UserId}",
                session.Id, amountPerMinute, session.UserId);

            // Check số dư
            var balance = await GetBalanceAsync(walletClient, session.UserId, ct);
            _logger.LogInformation("User {UserId} balance: {Balance}đ", session.UserId, balance);

            if (balance <= 0)
            {
                _logger.LogWarning("User {UserId} has 0 balance, closing session", session.UserId);
                session.Close();
                await context.SaveChangesAsync(ct);
                await ReleaseMachineAsync(machineClient, session.MachineId, ct);
                continue;
            }

            var deductAmount = balance < amountPerMinute ? balance : amountPerMinute;
            var success = await DeductAsync(walletClient, session.UserId, deductAmount, session.Id, ct);

            if (success)
            {
                session.AccumulateCost(deductAmount);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("Deducted {Amount}đ from user {UserId}, TotalCost now: {TotalCost}đ",
                    deductAmount, session.UserId, session.TotalCost);
            }

            // Đóng phiên nếu hết tiền sau lần trừ này
            if (balance <= amountPerMinute)
            {
                _logger.LogWarning("User {UserId} balance depleted, closing session", session.UserId);
                session.Close();
                await context.SaveChangesAsync(ct);
                await ReleaseMachineAsync(machineClient, session.MachineId, ct);
            }
        }
    }

    private async Task<decimal> GetBalanceAsync(HttpClient client, Guid userId, CancellationToken ct)
    {
        try
        {
            var res = await client.GetAsync($"/api/wallet/internal/{userId}", ct);
            if (!res.IsSuccessStatusCode) return 0;
            var json = await res.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("balance").GetDecimal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get balance for user {UserId}", userId);
            return 0;
        }
    }

    private async Task<bool> DeductAsync(HttpClient client, Guid userId, decimal amount, Guid sessionId, CancellationToken ct)
    {
        try
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                userId,
                amount,
                note = $"Phí chơi - phiên {sessionId}"
            });
            var res = await client.PostAsync("/api/wallet/internal/deduct",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Deduct failed for user {UserId}: {Error}", userId, err);
            }
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deduct error for user {UserId}", userId);
            return false;
        }
    }

    private async Task ReleaseMachineAsync(HttpClient client, Guid machineId, CancellationToken ct)
    {
        try { await client.PostAsync($"/api/machines/{machineId}/release", null, ct); }
        catch { }
    }
}
