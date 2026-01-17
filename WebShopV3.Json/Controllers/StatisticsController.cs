// Controllers/StatisticsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using WebShopV3.Json.Models.Statistics;
using WebShopV3.Json.Services;

namespace WebShopV3.Json.Controllers
{
    [Authorize(Roles = "Админ,Менеджер")]
    public class StatisticsController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(
            IStatisticsService statisticsService,
            ILogger<StatisticsController> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }

        // GET: /Statistics/Dashboard - Основная панель статистики
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Получаем статистику за последние 30 дней
                var request = new StatisticsRequest
                {
                    StartDate = DateTime.Today.AddDays(-30),
                    EndDate = DateTime.Today,
                    PeriodType = "month",
                    TopProductsCount = 10,
                    IncludeDetails = true
                };

                var stats = await _statisticsService.GetSalesStatisticsAsync(request);
                var financialSummary = await _statisticsService.GetFinancialSummaryAsync();
                var monthlySales = await _statisticsService.GetMonthlySalesAsync(DateTime.Today.Year);

                ViewBag.FinancialSummary = financialSummary;
                ViewBag.MonthlySales = monthlySales;
                ViewBag.CurrentYear = DateTime.Today.Year;

                // Данные для графиков
                ViewBag.ChartData = GetChartData(stats);

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке панели статистики");
                TempData["ErrorMessage"] = "Не удалось загрузить статистику";
                return View(new SalesStatistics());
            }
        }

        // GET: /Statistics/Sales - Детальная статистика продаж
        public async Task<IActionResult> Sales(string? period = "month", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var request = CreateStatisticsRequest(period, startDate, endDate);
                var stats = await _statisticsService.GetSalesStatisticsAsync(request);

                ViewBag.Period = period;
                ViewBag.StartDate = request.StartDate;
                ViewBag.EndDate = request.EndDate;
                ViewBag.AvailablePeriods = GetAvailablePeriods();

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке статистики продаж");
                TempData["ErrorMessage"] = "Не удалось загрузить статистику продаж";
                return View(new SalesStatistics());
            }
        }

        // GET: /Statistics/TopProducts - Топ товаров
        public async Task<IActionResult> TopProducts(string? productType = null, int? limit = null)
        {
            try
            {
                var topProducts = await _statisticsService.GetTopProductsAsync(limit ?? 20, productType);

                ViewBag.ProductType = productType ?? "all";
                ViewBag.Limit = limit ?? 20;
                ViewBag.AvailableLimits = new[] { 10, 20, 50, 100 };

                return View(topProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке топ товаров");
                TempData["ErrorMessage"] = "Не удалось загрузить топ товаров";
                return View(new List<TopProduct>());
            }
        }

        // GET: /Statistics/Financial - Финансовая аналитика
        public async Task<IActionResult> Financial()
        {
            try
            {
                var financialSummary = await _statisticsService.GetFinancialSummaryAsync();
                var monthlySales = await _statisticsService.GetMonthlySalesAsync(DateTime.Today.Year);
                var yearlySales = new List<MonthlySales>();

                // За последние 3 года
                for (int year = DateTime.Today.Year - 2; year <= DateTime.Today.Year; year++)
                {
                    var yearly = await _statisticsService.GetMonthlySalesAsync(year);
                    yearlySales.AddRange(yearly);
                }

                ViewBag.MonthlySales = monthlySales;
                ViewBag.YearlySales = yearlySales;
                ViewBag.CurrentYear = DateTime.Today.Year;

                return View(financialSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке финансовой аналитики");
                TempData["ErrorMessage"] = "Не удалось загрузить финансовую аналитику";
                return View(new FinancialSummary());
            }
        }

        // GET: /Statistics/Daily - Ежедневные продажи
        public async Task<IActionResult> Daily(DateTime? date = null)
        {
            try
            {
                date ??= DateTime.Today;
                var startDate = date.Value.AddDays(-30);
                var dailySales = await _statisticsService.GetDailySalesAsync(startDate, date.Value);

                ViewBag.SelectedDate = date;
                ViewBag.StartDate = startDate;

                return View(dailySales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке ежедневных продаж");
                TempData["ErrorMessage"] = "Не удалось загрузить ежедневные продажи";
                return View(new List<DailySales>());
            }
        }

        // POST: /Statistics/Export - Экспорт отчета
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(ExportRequest request)
        {
            try
            {
                byte[] fileBytes;
                string fileName;
                string contentType;

                switch (request.ExportType.ToLower())
                {
                    case "excel":
                        fileBytes = await _statisticsService.ExportToExcelAsync(request);
                        fileName = $"Отчет_продажи_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.xlsx";
                        contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        break;

                    case "csv":
                        fileBytes = await _statisticsService.ExportToCsvAsync(request);
                        fileName = $"Отчет_продажи_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.csv";
                        contentType = "text/csv";
                        break;

                    default:
                        TempData["ErrorMessage"] = "Неподдерживаемый формат экспорта";
                        return RedirectToAction("Sales");
                }

                _logger.LogInformation("Экспортирован отчет: {FileName}", fileName);
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте отчета");
                TempData["ErrorMessage"] = "Ошибка при экспорте отчета";
                return RedirectToAction("Sales");
            }
        }

        // GET: /Statistics/GenerateReport - Генерация комплексного отчета
        public async Task<IActionResult> GenerateReport(string? period = "month", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var request = new StatisticsRequest
                {
                    StartDate = startDate ?? DateTime.Today.AddMonths(-1),
                    EndDate = endDate ?? DateTime.Today,
                    PeriodType = period ?? "month",
                    TopProductsCount = 20,
                    IncludeDetails = true
                };

                var stream = await _statisticsService.GenerateSalesReportAsync(request);

                var fileName = $"Полный_отчет_{request.StartDate:yyyyMMdd}_{request.EndDate:yyyyMMdd}.xlsx";

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации отчета");
                TempData["ErrorMessage"] = "Ошибка при генерации отчета";
                return RedirectToAction("Dashboard");
            }
        }

        // AJAX: /Statistics/GetChartData - Данные для графиков
        [HttpGet]
        public async Task<IActionResult> GetChartData(string chartType, string period = "month")
        {
            try
            {
                var request = CreateStatisticsRequest(period, null, null);
                var stats = await _statisticsService.GetSalesStatisticsAsync(request);

                object chartData = chartType.ToLower() switch
                {
                    "revenue" => GetRevenueChartData(stats),
                    "products" => GetProductsChartData(stats),
                    "top" => GetTopProductsChartData(stats),
                    "daily" => GetDailyChartData(stats),
                    _ => new { error = "Неизвестный тип графика" }
                };

                return Json(new { success = true, data = chartData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных для графика");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Вспомогательные методы
        private StatisticsRequest CreateStatisticsRequest(string period, DateTime? startDate, DateTime? endDate)
        {
            var request = new StatisticsRequest { PeriodType = period };

            switch (period.ToLower())
            {
                case "today":
                    request.StartDate = DateTime.Today;
                    request.EndDate = DateTime.Today;
                    break;

                case "week":
                    request.StartDate = DateTime.Today.AddDays(-7);
                    request.EndDate = DateTime.Today;
                    break;

                case "month":
                    request.StartDate = DateTime.Today.AddMonths(-1);
                    request.EndDate = DateTime.Today;
                    break;

                case "quarter":
                    request.StartDate = DateTime.Today.AddMonths(-3);
                    request.EndDate = DateTime.Today;
                    break;

                case "year":
                    request.StartDate = DateTime.Today.AddYears(-1);
                    request.EndDate = DateTime.Today;
                    break;

                case "custom":
                    request.StartDate = startDate ?? DateTime.Today.AddMonths(-1);
                    request.EndDate = endDate ?? DateTime.Today;
                    break;

                default:
                    request.StartDate = DateTime.Today.AddMonths(-1);
                    request.EndDate = DateTime.Today;
                    break;
            }

            return request;
        }

        private Dictionary<string, string> GetAvailablePeriods()
        {
            return new Dictionary<string, string>
            {
                { "today", "Сегодня" },
                { "week", "Неделя" },
                { "month", "Месяц" },
                { "quarter", "Квартал" },
                { "year", "Год" },
                { "custom", "Произвольный период" }
            };
        }

        private object GetChartData(SalesStatistics stats)
        {
            return new
            {
                revenue = GetRevenueChartData(stats),
                products = GetProductsChartData(stats),
                top = GetTopProductsChartData(stats)
            };
        }

        private object GetRevenueChartData(SalesStatistics stats)
        {
            return new
            {
                labels = stats.DailySales.Select(d => d.Date.ToString("dd.MM")).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "Выручка (₽)",
                        data = stats.DailySales.Select(d => (double)d.Revenue).ToList(),
                        borderColor = "rgb(75, 192, 192)",
                        backgroundColor = "rgba(75, 192, 192, 0.2)"
                    }
                }
            };
        }

        private object GetProductsChartData(SalesStatistics stats)
        {
            return new
            {
                labels = new[] { "Компьютеры", "Комплектующие" },
                datasets = new[]
                {
                    new
                    {
                        data = new[] { stats.TotalComputersSold, stats.TotalComponentsSold },
                        backgroundColor = new[] { "rgb(255, 99, 132)", "rgb(54, 162, 235)" }
                    }
                }
            };
        }

        private object GetTopProductsChartData(SalesStatistics stats)
        {
            var top10 = stats.TopProducts.Take(10).ToList();

            return new
            {
                labels = top10.Select(p => p.Name.Length > 20 ? p.Name.Substring(0, 20) + "..." : p.Name).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "Выручка (₽)",
                        data = top10.Select(p => (double)p.Revenue).ToList(),
                        backgroundColor = "rgba(54, 162, 235, 0.5)"
                    }
                }
            };
        }

        private object GetDailyChartData(SalesStatistics stats)
        {
            return new
            {
                labels = stats.DailySales.Select(d => d.Date.ToString("dd.MM")).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "Заказы",
                        data = stats.DailySales.Select(d => (double)d.OrdersCount).ToList(),
                        borderColor = "rgb(255, 99, 132)",
                        backgroundColor = "rgba(255, 99, 132, 0.2)"
                    },
                    new
                    {
                        label = "Товары",
                        data = stats.DailySales.Select(d => (double)d.ProductsSold).ToList(),
                        borderColor = "rgb(54, 162, 235)",
                        backgroundColor = "rgba(54, 162, 235, 0.2)"
                    }
                }
            };
        }
    }
}