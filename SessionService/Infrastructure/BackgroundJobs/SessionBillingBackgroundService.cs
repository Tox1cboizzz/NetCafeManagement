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
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Lỗi billing"); }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SessionDbContext>();

        var sessions = await context.Sessions
            .Where(s => s.Status == SessionStatus.Active)
            .ToListAsync(ct);

        if (!sessions.Any()) return;

        var walletClient = _httpClientFactory.CreateClient("WalletService");
        var machineClient = _httpClientFactory.CreateClient("MachineService");

        foreach (var session in sessions)
        {
            // Tiền mỗi phút = PricePerHour / 60 * (1 - Discount)
            var amountPerMinute = Math.Round((session.PricePerHour / 60m) * (1 - session.Discount), 0);
            if (amountPerMinute <= 0) continue;

            // Check số dư trước
            var balance = await GetBalanceAsync(walletClient, session.UserId, ct);

            if (balance <= 0)
            {
                // Hết tiền → đóng phiên + khóa máy
                _logger.LogWarning("User {UserId} hết tiền, đóng session {SessionId}", session.UserId, session.Id);
                session.Close();
                await context.SaveChangesAsync(ct);
                await ReleaseMachineAsync(machineClient, session.MachineId, ct);
                continue;
            }

            // Nếu số dư < tiền 1 phút → trừ hết số dư còn lại rồi đóng
            var deductAmount = balance < amountPerMinute ? balance : amountPerMinute;
            var success = await DeductAsync(walletClient, session.UserId, deductAmount, session.Id, ct);

            if (!success || balance <= amountPerMinute)
            {
                _logger.LogWarning("User {UserId} hết tiền sau phút này, đóng session {SessionId}", session.UserId, session.Id);
                session.AccumulateCost(deductAmount);
                session.Close();
                await context.SaveChangesAsync(ct);
                await ReleaseMachineAsync(machineClient, session.MachineId, ct);
            }
            else
            {
                session.AccumulateCost(deductAmount);
                await context.SaveChangesAsync(ct);
            }
        }
    }

    private async Task<decimal> GetBalanceAsync(HttpClient client, Guid userId, CancellationToken ct)
    {
        try
        {
            var res = await client.GetAsync($"/api/wallet/{userId}", ct);
            if (!res.IsSuccessStatusCode) return 0;
            var json = await res.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("balance").GetDecimal();
        }
        catch { return 0; }
    }

    private async Task<bool> DeductAsync(HttpClient client, Guid userId, decimal amount, Guid sessionId, CancellationToken ct)
    {
        try
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                userId,
                amount,
                note = $"Phí chơi máy - phiên {sessionId}"
            });
            var res = await client.PostAsync("/api/wallet/deduct",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task ReleaseMachineAsync(HttpClient client, Guid machineId, CancellationToken ct)
    {
        try { await client.PostAsync($"/api/machines/{machineId}/release", null, ct); }
        catch { }
    }
}
