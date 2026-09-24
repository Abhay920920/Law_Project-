using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<NotificationBackgroundService> _logger;

        public NotificationBackgroundService(IServiceProvider services, ILogger<NotificationBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification & eCourts Background Service starting.");

            try
            {
                // Allow application to fully initialize before background sync begins
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using (var scope = _services.CreateScope())
                        {
                            var dbHelper = scope.ServiceProvider.GetRequiredService<DBHelper>();

                            // Multi-instance distributed coordination: Acquire lock via sp_getapplock
                            using var conn = dbHelper.GetConnection();
                            await conn.OpenAsync(stoppingToken);

                            bool lockAcquired = false;
                            try
                            {
                                using (var lockCmd = new SqlCommand("sp_getapplock", conn))
                                {
                                    lockCmd.CommandType = CommandType.StoredProcedure;
                                    lockCmd.Parameters.AddWithValue("@Resource", "LawProject_BackgroundNotificationSync_Lock");
                                    lockCmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                                    lockCmd.Parameters.AddWithValue("@LockOwner", "Session");
                                    lockCmd.Parameters.AddWithValue("@LockTimeout", 0); // Non-blocking: if held, skip immediately

                                    var returnParam = lockCmd.Parameters.Add("@Result", SqlDbType.Int);
                                    returnParam.Direction = ParameterDirection.ReturnValue;

                                    await lockCmd.ExecuteNonQueryAsync(stoppingToken);
                                    int lockResult = (int)returnParam.Value;
                                    lockAcquired = lockResult >= 0;
                                }

                                if (!lockAcquired)
                                {
                                    _logger.LogInformation("Another application instance is currently executing background sync. Skipping run on this instance.");
                                }
                                else
                                {
                                    _logger.LogInformation("Acquired background sync lock. Executing daily alerts and e-Courts sync...");

                                    var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                                    notificationRepo.GenerateDailyAlerts();
                                    _logger.LogInformation("Daily notifications generated successfully at {Time}", DateTime.UtcNow);

                                    var ecourtsRepo = scope.ServiceProvider.GetRequiredService<IECourtsRepository>();
                                    var napixService = scope.ServiceProvider.GetRequiredService<IECourtsNapixService>();

                                    // Sync registered CNR tracked cases next hearing date & current status
                                    var trackedCases = await ecourtsRepo.GetAllTrackedCasesAsync();
                                    int syncedCount = 0;

                                    foreach (var c in trackedCases)
                                    {
                                        if (stoppingToken.IsCancellationRequested) break;

                                        var cleanCnr = !string.IsNullOrEmpty(c.CNRNumber) 
                                            ? System.Text.RegularExpressions.Regex.Replace(c.CNRNumber.Trim(), @"[^A-Za-z0-9]", "").ToUpperInvariant() 
                                            : null;

                                        if (!string.IsNullOrEmpty(cleanCnr) && cleanCnr.Length >= 12 && cleanCnr.Length <= 18)
                                        {
                                            try
                                            {
                                                var liveStatus = await napixService.GetCnrDetailsAsync(cleanCnr, c.IsHighCourt);
                                                if (liveStatus.HasValue && liveStatus.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                                                {
                                                    if (liveStatus.Value.TryGetProperty("next_date", out var nextDateProp) && DateTime.TryParse(nextDateProp.GetString(), out DateTime nextDt))
                                                    {
                                                        c.NextHearingDate = nextDt;
                                                    }
                                                    if (liveStatus.Value.TryGetProperty("stage", out var stageProp))
                                                    {
                                                        c.CurrentStage = stageProp.GetString();
                                                    }
                                                    await ecourtsRepo.SaveTrackedCaseAsync(c);
                                                    syncedCount++;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogWarning("Error syncing tracked CNR {CNR}: {Msg}", cleanCnr, ex.Message);
                                            }

                                            await Task.Delay(1000, stoppingToken);
                                        }
                                    }

                                    _logger.LogInformation("Completed background sync: {Count} cases synchronized with e-Courts.", syncedCount);
                                }
                            }
                            finally
                            {
                                if (lockAcquired)
                                {
                                    try
                                    {
                                        using var releaseCmd = new SqlCommand("sp_releaseapplock", conn);
                                        releaseCmd.CommandType = CommandType.StoredProcedure;
                                        releaseCmd.Parameters.AddWithValue("@Resource", "LawProject_BackgroundNotificationSync_Lock");
                                        releaseCmd.Parameters.AddWithValue("@LockOwner", "Session");
                                        await releaseCmd.ExecuteNonQueryAsync(CancellationToken.None);
                                    }
                                    catch { /* non-critical */ }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing background notification sync cycle.");
                    }

                    // Check again every hour, catching cancellation gracefully on host shutdown
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Notification & eCourts Background Service stopped gracefully.");
            }
        }
    }
}
