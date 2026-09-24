using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MVCCaseManagement.DAL;
using MVCCaseManagement.Models;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Services
{
    public class ECourtsSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ECourtsSyncBackgroundService> _logger;

        public ECourtsSyncBackgroundService(IServiceProvider serviceProvider, ILogger<ECourtsSyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ECourtsSyncBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    
                    // Morning Cause List Ingestion at 06:00 AM (run if current hour is 6)
                    if (now.Hour == 6 && now.Minute < 15)
                    {
                        _logger.LogInformation("Executing morning Cause List Sync at {Time}", now);
                        await SyncTodayCauseListsAsync();
                    }

                    // Evening Next Hearing & CNR Sync at 08:00 PM (run if current hour is 20)
                    if (now.Hour == 20 && now.Minute < 15)
                    {
                        _logger.LogInformation("Executing evening CNR Hearing Sync at {Time}", now);
                        await SyncTrackedCasesCNRAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ECourtsSyncBackgroundService loop");
                }

                // Check every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task SyncTodayCauseListsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var eCourtsService = scope.ServiceProvider.GetRequiredService<IECourtsService>();
            var eCourtsRepo = scope.ServiceProvider.GetRequiredService<IECourtsRepository>();

            try
            {
                // Retrieve all unique establishments from tracked cases
                var trackedCases = (await eCourtsRepo.GetAllTrackedCasesAsync()).ToList();
                var establishments = trackedCases.Select(c => c.EstCode).Where(e => !string.IsNullOrEmpty(e)).Distinct();

                string dateStr = DateTime.Now.ToString("yyyy-MM-dd");

                foreach (var estCode in establishments)
                {
                    var res = await eCourtsService.GetCauseListAsync(estCode, "1", dateStr, "civil");
                    if (res.Success && res.Data != null)
                    {
                        var items = res.Data.Select(d => new CauselistItemModel
                        {
                            SrNo = d.ItemNo,
                            CNRNumber = d.CNRNumber,
                            CaseNumber = d.CaseNumber,
                            PartyDetails = $"{d.Petitioner} vs {d.Respondent}",
                            AdvocateDetails = d.AdvocateName,
                            Stage = d.Stage,
                            IsCorporationCase = d.IsCorpCase
                        }).ToList();

                        var header = new CauselistHeaderModel
                        {
                            EstCode = estCode,
                            CourtNo = "1",
                            CauselistDate = DateTime.Now.Date,
                            CauselistType = "civil",
                            TotalCases = items.Count,
                            CorporationCasesCount = items.Count(i => i.IsCorporationCase),
                            Items = items
                        };
                        await eCourtsRepo.SaveCauselistAsync(header);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing today cause lists in background service");
            }
        }

        private async Task SyncTrackedCasesCNRAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var eCourtsService = scope.ServiceProvider.GetRequiredService<IECourtsService>();
            var eCourtsRepo = scope.ServiceProvider.GetRequiredService<IECourtsRepository>();

            try
            {
                var trackedCases = (await eCourtsRepo.GetAllTrackedCasesAsync()).ToList();
                foreach (var kase in trackedCases)
                {
                    if (string.IsNullOrEmpty(kase.CNRNumber)) continue;

                    var res = await eCourtsService.FetchCaseByCNRAsync(kase.CNRNumber);
                    if (res.Success && res.Data != null)
                    {
                        string status = res.Data.TryGetValue("case_status", out var sObj) ? sObj?.ToString() ?? "" : "";
                        string stage = res.Data.TryGetValue("purpose_name", out var pObj) ? pObj?.ToString() ?? "" : "";
                        DateTime? nextDate = null;
                        if (res.Data.TryGetValue("next_date", out var dObj) && dObj != null)
                        {
                            if (DateTime.TryParse(dObj.ToString(), out DateTime parsed))
                            {
                                nextDate = parsed;
                            }
                        }

                        await eCourtsRepo.UpdateTrackedCaseStatusAsync(kase.CNRNumber, status, nextDate, stage);
                    }

                    // Throttle API requests slightly to avoid rate limit
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing CNR tracked cases in background service");
            }
        }
    }
}
