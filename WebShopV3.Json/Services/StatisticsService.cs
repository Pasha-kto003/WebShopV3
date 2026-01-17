// Services/StatisticsService.cs
using Microsoft.Extensions.Caching.Memory;
using Spire.Xls;
using System.Drawing;
using System.Globalization;
using WebShopV3.Json.Models;
using WebShopV3.Json.Models.Statistics;
using Order = WebShopV3.Json.Models.Order;
using OrderType = WebShopV3.Json.Models.OrderType;

namespace WebShopV3.Json.Services
{
    public interface IStatisticsService
    {
        Task<SalesStatistics> GetSalesStatisticsAsync(StatisticsRequest request);
        Task<FinancialSummary> GetFinancialSummaryAsync();
        Task<List<TopProduct>> GetTopProductsAsync(int limit = 10, string? productType = null);
        Task<List<DailySales>> GetDailySalesAsync(DateTime startDate, DateTime endDate);
        Task<List<MonthlySales>> GetMonthlySalesAsync(int? year = null);
        Task<byte[]> ExportToExcelAsync(ExportRequest request);
        Task<byte[]> ExportToCsvAsync(ExportRequest request);
        Task<MemoryStream> GenerateSalesReportAsync(StatisticsRequest request);
    }

    public class StatisticsService : IStatisticsService
    {
        private readonly JsonDataService _jsonData;
        private readonly ILogger<StatisticsService> _logger;
        private readonly IMemoryCache _cache;
        private const string CACHE_PREFIX = "stats_";

        public StatisticsService(
            JsonDataService jsonData,
            ILogger<StatisticsService> logger,
            IMemoryCache cache)
        {
            _jsonData = jsonData;
            _logger = logger;
            _cache = cache;
        }

        public async Task<SalesStatistics> GetSalesStatisticsAsync(StatisticsRequest request)
        {
            var cacheKey = $"{CACHE_PREFIX}sales_{request.StartDate}_{request.EndDate}_{request.PeriodType}_{request.TopProductsCount}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                var statistics = new SalesStatistics
                {
                    PeriodStart = request.StartDate ?? DateTime.Today.AddMonths(-1),
                    PeriodEnd = request.EndDate ?? DateTime.Today
                };

                try
                {
                    // Загружаем все необходимые данные
                    var orders = await GetCompletedOrdersInPeriodAsync(statistics.PeriodStart, statistics.PeriodEnd);
                    var computerOrders = await _jsonData.GetAllAsync<ComputerOrder>("ComputerOrders");
                    var componentOrders = await _jsonData.GetAllAsync<ComponentOrder>("ComponentOrders");
                    var computers = await _jsonData.GetAllAsync<Computer>("Computers");
                    var components = await _jsonData.GetAllAsync<Component>("Components");

                    // Расчет основных показателей
                    await CalculateFinancialMetricsAsync(statistics, orders);
                    await CalculateQuantityMetricsAsync(statistics, orders, computerOrders, componentOrders);
                    await CalculateTopProductsAsync(statistics, computerOrders, componentOrders, computers, components, request.TopProductsCount ?? 10);
                    await CalculateDailySalesAsync(statistics, orders, computerOrders, componentOrders);
                    await CalculateOrderTypeDistributionAsync(statistics, orders);

                    // Сравнение с предыдущим периодом
                    await CalculateChangesAsync(statistics, request);

                    return statistics;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при расчете статистики");
                    return statistics;
                }
            }) ?? new SalesStatistics();
        }

        private async Task<List<Order>> GetCompletedOrdersInPeriodAsync(DateTime startDate, DateTime endDate)
        {
            var orders = await _jsonData.GetAllAsync<Order>("orders");
            var orderstatuses = await _jsonData.GetAllAsync<Status>("orderstatuses");

            var completedStatus = orderstatuses.FirstOrDefault(s => s.Name == "Завершен")?.Id ?? 4;

            return orders
                .Where(o => o.StatusId == completedStatus &&
                            o.OrderDate >= startDate &&
                            o.OrderDate <= endDate.AddDays(1))
                .ToList();
        }

        private async Task CalculateFinancialMetricsAsync(SalesStatistics statistics, List<Order> orders)
        {
            statistics.TotalOrders = orders.Count;
            statistics.TotalRevenue = orders.Sum(o => o.TotalAmount);

            // Расчет себестоимости (здесь упрощенно - 70% от выручки)
            // В реальном приложении нужно брать реальные закупочные цены
            statistics.TotalCost = statistics.TotalRevenue * 0.7m;
        }

        private async Task CalculateQuantityMetricsAsync(
            SalesStatistics statistics,
            List<Order> orders,
            List<ComputerOrder> computerOrders,
            List<ComponentOrder> componentOrders)
        {
            var orderIds = orders.Select(o => o.Id).ToList();

            var orderComputerOrders = computerOrders
                .Where(co => orderIds.Contains(co.OrderId))
                .ToList();

            var orderComponentOrders = componentOrders
                .Where(co => orderIds.Contains(co.OrderId))
                .ToList();

            statistics.TotalComputersSold = orderComputerOrders.Sum(co => co.Quantity);
            statistics.TotalComponentsSold = orderComponentOrders.Sum(co => co.Quantity);
            statistics.TotalProductsSold = statistics.TotalComputersSold + statistics.TotalComponentsSold;
        }

        private async Task CalculateTopProductsAsync(
            SalesStatistics statistics,
            List<ComputerOrder> computerOrders,
            List<ComponentOrder> componentOrders,
            List<Computer> computers,
            List<Component> components,
            int topCount)
        {
            // Топ компьютеров
            var computerSales = computerOrders
                .GroupBy(co => co.ComputerId)
                .Select(g => new
                {
                    ComputerId = g.Key,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topCount)
                .ToList();

            statistics.TopComputers = computerSales
                .Select((s, index) => new TopProduct
                {
                    Id = s.ComputerId,
                    Name = computers.FirstOrDefault(c => c.Id == s.ComputerId)?.Name ?? $"Компьютер #{s.ComputerId}",
                    Type = "Computer",
                    QuantitySold = s.QuantitySold,
                    Revenue = s.Revenue,
                    Rank = index + 1,
                    MarketSharePercent = statistics.TotalRevenue > 0 ? (s.Revenue / statistics.TotalRevenue) * 100 : 0
                })
                .ToList();

            // Топ компонентов
            var componentSales = componentOrders
                .GroupBy(co => co.ComponentId)
                .Select(g => new
                {
                    ComponentId = g.Key,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(topCount)
                .ToList();

            statistics.TopComponents = componentSales
                .Select((s, index) =>
                {
                    var component = components.FirstOrDefault(c => c.Id == s.ComponentId);
                    return new TopProduct
                    {
                        Id = s.ComponentId,
                        Name = component?.Name ?? $"Компонент #{s.ComponentId}",
                        Type = "Component",
                        ComponentType = component?.Type,
                        QuantitySold = s.QuantitySold,
                        Revenue = s.Revenue,
                        Rank = index + 1,
                        MarketSharePercent = statistics.TotalRevenue > 0 ? (s.Revenue / statistics.TotalRevenue) * 100 : 0
                    };
                })
                .ToList();

            // Общий топ товаров
            statistics.TopProducts = statistics.TopComputers
                .Concat(statistics.TopComponents)
                .OrderByDescending(p => p.Revenue)
                .Take(topCount)
                .ToList();
        }

        private async Task CalculateDailySalesAsync(
            SalesStatistics statistics,
            List<Order> orders,
            List<ComputerOrder> computerOrders,
            List<ComponentOrder> componentOrders)
        {
            var dailySales = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new DailySales
                {
                    Date = g.Key,
                    OrdersCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(d => d.Date)
                .ToList();

            // Заполняем пропущенные дни нулями
            var allDates = Enumerable.Range(0, (statistics.PeriodEnd - statistics.PeriodStart).Days + 1)
                .Select(offset => statistics.PeriodStart.AddDays(offset).Date)
                .ToList();

            statistics.DailySales = allDates
                .Select(date =>
                {
                    var existing = dailySales.FirstOrDefault(d => d.Date == date);
                    return existing ?? new DailySales
                    {
                        Date = date,
                        OrdersCount = 0,
                        Revenue = 0,
                        ProductsSold = 0,
                        ComputersSold = 0,
                        ComponentsSold = 0
                    };
                })
                .ToList();
        }

        public async Task<byte[]> ExportToCsvAsync(ExportRequest request)
        {
            var stats = await GetSalesStatisticsAsync(new StatisticsRequest
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate
            });

            var csvLines = new List<string>
            {
                $"Отчет о продажах за период с {request.StartDate:dd.MM.yyyy} по {request.EndDate:dd.MM.yyyy}",
                "",
                "Основные показатели",
                $"Общая выручка;{stats.TotalRevenue:N2}",
                $"Себестоимость;{stats.TotalCost:N2}",
                $"Прибыль;{stats.Profit:N2}",
                $"Рентабельность;{stats.ProfitMargin:F2}%",
                $"Количество заказов;{stats.TotalOrders}",
                "",
                "Топ товаров",
                "№;Товар;Тип;Продано шт.;Выручка;Доля рынка %"
            };

            foreach (var product in stats.TopProducts.Take(10))
            {
                csvLines.Add($"{product.Rank};{product.Name};{product.Type};{product.QuantitySold};{product.Revenue:N2};{product.MarketSharePercent:F2}%");
            }

            return System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, csvLines));
        }


        private async Task CalculateOrderTypeDistributionAsync(SalesStatistics statistics, List<Order> orders)
        {
            var orderTypes = await _jsonData.GetAllAsync<OrderType>("OrderTypes");

            var distribution = orders
                .GroupBy(o => o.OrderTypeId)
                .Select(g => new
                {
                    OrderTypeId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            foreach (var item in distribution)
            {
                var orderType = orderTypes.FirstOrDefault(ot => ot.Id == item.OrderTypeId);
                if (orderType != null)
                {
                    statistics.OrderTypeDistribution[orderType.Name] = item.Count;
                }
            }
        }

        private async Task CalculateChangesAsync(SalesStatistics statistics, StatisticsRequest request)
        {
            try
            {
                var previousPeriodStart = statistics.PeriodStart.AddDays(-(statistics.PeriodEnd - statistics.PeriodStart).Days);
                var previousPeriodEnd = statistics.PeriodStart.AddDays(-1);

                var previousRequest = new StatisticsRequest
                {
                    StartDate = previousPeriodStart,
                    EndDate = previousPeriodEnd,
                    PeriodType = request.PeriodType
                };

                var previousStats = await GetSalesStatisticsAsync(previousRequest);

                if (previousStats.TotalRevenue > 0)
                {
                    statistics.RevenueChangePercent = ((statistics.TotalRevenue - previousStats.TotalRevenue) / previousStats.TotalRevenue) * 100;
                }

                if (previousStats.TotalOrders > 0)
                {
                    statistics.OrdersChangePercent = (int)(((double)(statistics.TotalOrders - previousStats.TotalOrders) / previousStats.TotalOrders) * 100);
                }
            }
            catch
            {
                // Игнорируем ошибки при расчете изменений
            }
        }

        public async Task<FinancialSummary> GetFinancialSummaryAsync()
        {
            var cacheKey = $"{CACHE_PREFIX}financial_summary";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                var summary = new FinancialSummary();
                var orders = await _jsonData.GetAllAsync<Order>("Orders");
                var completedStatusId = (await _jsonData.GetAllAsync<Status>("orderstatuses"))
                    .FirstOrDefault(s => s.Name == "Выполнен")?.Id ?? 4;

                // Все выполненные заказы
                var completedOrders = orders.Where(o => o.StatusId == completedStatusId).ToList();

                summary.TotalRevenue = completedOrders.Sum(o => o.TotalAmount);
                summary.TotalCost = summary.TotalRevenue * 0.7m; // Упрощенный расчет себестоимости

                // Текущий месяц
                var thisMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var thisMonthEnd = thisMonthStart.AddMonths(1).AddDays(-1);

                var thisMonthOrders = completedOrders
                    .Where(o => o.OrderDate >= thisMonthStart && o.OrderDate <= thisMonthEnd)
                    .ToList();

                summary.ThisMonthRevenue = thisMonthOrders.Sum(o => o.TotalAmount);
                summary.ThisMonthCost = summary.ThisMonthRevenue * 0.7m;

                // Прошлый месяц
                var lastMonthStart = thisMonthStart.AddMonths(-1);
                var lastMonthEnd = thisMonthStart.AddDays(-1);

                var lastMonthOrders = completedOrders
                    .Where(o => o.OrderDate >= lastMonthStart && o.OrderDate <= lastMonthEnd)
                    .ToList();

                summary.LastMonthRevenue = lastMonthOrders.Sum(o => o.TotalAmount);
                summary.LastMonthCost = summary.LastMonthRevenue * 0.7m;

                return summary;
            }) ?? new FinancialSummary();
        }

        public async Task<List<TopProduct>> GetTopProductsAsync(int limit = 10, string? productType = null)
        {
            var request = new StatisticsRequest
            {
                StartDate = DateTime.Today.AddMonths(-3), // Последние 3 месяца
                EndDate = DateTime.Today,
                TopProductsCount = limit,
                ProductType = productType
            };

            var stats = await GetSalesStatisticsAsync(request);

            return productType?.ToLower() switch
            {
                "computers" => stats.TopComputers.Take(limit).ToList(),
                "components" => stats.TopComponents.Take(limit).ToList(),
                _ => stats.TopProducts.Take(limit).ToList()
            };
        }

        public async Task<List<DailySales>> GetDailySalesAsync(DateTime startDate, DateTime endDate)
        {
            var request = new StatisticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                PeriodType = "custom"
            };

            var stats = await GetSalesStatisticsAsync(request);
            return stats.DailySales;
        }

        public async Task<List<MonthlySales>> GetMonthlySalesAsync(int? year = null)
        {
            year ??= DateTime.Today.Year;

            var monthlySales = new List<MonthlySales>();

            for (int month = 1; month <= 12; month++)
            {
                var startDate = new DateTime(year.Value, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var request = new StatisticsRequest
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    PeriodType = "month"
                };

                var stats = await GetSalesStatisticsAsync(request);

                // Создаем MonthlySales только с основными данными
                var monthly = new MonthlySales
                {
                    Year = year.Value,
                    Month = month,
                    OrdersCount = stats.TotalOrders,
                    Revenue = stats.TotalRevenue,
                    Cost = stats.TotalCost
                    // Profit и ProfitMargin вычисляются автоматически!
                };

                monthlySales.Add(monthly);
            }

            return monthlySales;
        }


        public async Task<byte[]> ExportToExcelAsync(ExportRequest request)
        {
            var stats = await GetSalesStatisticsAsync(new StatisticsRequest
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IncludeDetails = true
            });

            // Создаем рабочую книгу FreeSpire.XLS
            Workbook workbook = new Workbook();

            // Устанавливаем свойства документа
            workbook.DocumentProperties.Author = "WebShopV3";
            workbook.DocumentProperties.Title = $"Отчет о продажах {request.StartDate:dd.MM.yyyy}-{request.EndDate:dd.MM.yyyy}";
            workbook.DocumentProperties.Subject = "Статистика продаж";
            workbook.DocumentProperties.Company = "WebShopV3";

            // Создаем лист
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Отчет о продажах";

            // Настраиваем ширину колонок
            sheet.SetColumnWidth(1, 30);  // A - Заголовки
            sheet.SetColumnWidth(2, 20);  // B - Значения
            sheet.SetColumnWidth(3, 15);  // C - Единицы измерения

            int currentRow = 1;

            // Заголовок отчета
            CellRange titleRange = sheet.Range[currentRow, 1, currentRow, 3];
            titleRange.Merge();
            titleRange.Text = $"Отчет о продажах за период с {request.StartDate:dd.MM.yyyy} по {request.EndDate:dd.MM.yyyy}";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 14;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;
            titleRange.Style.VerticalAlignment = VerticalAlignType.Center;

            currentRow += 2;

            // Основные показатели
            sheet.Range[currentRow, 1].Text = "Основные показатели";
            sheet.Range[currentRow, 1].Style.Font.IsBold = true;
            sheet.Range[currentRow, 1].Style.Font.Size = 12;
            currentRow++;

            // Таблица основных показателей
            var metrics = new[]
            {
            new { Label = "Общая выручка", Value = stats.TotalRevenue, Format = "#,##0.00 ₽" },
            new { Label = "Себестоимость", Value = stats.TotalCost, Format = "#,##0.00 ₽" },
            new { Label = "Прибыль", Value = stats.Profit, Format = "#,##0.00 ₽" },
            new { Label = "Рентабельность", Value = stats.ProfitMargin, Format = "0.00 %" },
            new { Label = "Количество заказов", Value = (decimal)stats.TotalOrders, Format = "#,##0" },
            new { Label = "Товаров продано", Value = (decimal)stats.TotalProductsSold, Format = "#,##0" },
            new { Label = "Средний чек", Value = stats.AverageOrderValue, Format = "#,##0.00 ₽" },
            new { Label = "Изменение выручки", Value = stats.RevenueChangePercent, Format = "0.00 %" }
        };

            foreach (var metric in metrics)
            {
                sheet.Range[currentRow, 1].Text = metric.Label;
                sheet.Range[currentRow, 1].Style.Font.IsBold = true;

                var valueCell = sheet.Range[currentRow, 2];
                valueCell.NumberValue = (double)metric.Value;
                valueCell.NumberFormat = metric.Format;

                // Раскрашиваем прибыль/убыток
                if (metric.Label.Contains("Прибыль"))
                {
                    valueCell.Style.Font.Color = stats.Profit >= 0 ?
                        System.Drawing.Color.Green : System.Drawing.Color.Red;
                }
                else if (metric.Label.Contains("Рентабельность"))
                {
                    valueCell.Style.Font.Color = stats.ProfitMargin >= 20 ?
                        System.Drawing.Color.Green : System.Drawing.Color.Orange;
                }

                currentRow++;
            }

            currentRow++;

            // Топ товаров
            sheet.Range[currentRow, 1].Text = "Топ-10 товаров по выручке";
            sheet.Range[currentRow, 1].Style.Font.IsBold = true;
            sheet.Range[currentRow, 1].Style.Font.Size = 12;
            currentRow++;

            // Заголовки таблицы топ товаров
            string[] headers = { "№", "Товар", "Тип", "Продано шт.", "Выручка ₽", "Доля рынка %" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Range[currentRow, i + 1];
                cell.Text = headers[i];
                cell.Style.Font.IsBold = true;
                cell.Style.Color = System.Drawing.Color.LightGray;
                cell.Style.Borders.LineStyle = LineStyleType.Thin;
                cell.Style.Borders.Color = System.Drawing.Color.Black;
                cell.Style.HorizontalAlignment = HorizontalAlignType.Center;
            }

            currentRow++;

            // Данные топ товаров
            int rank = 1;
            foreach (var product in stats.TopProducts.Take(10))
            {
                sheet.Range[currentRow, 1].NumberValue = rank;
                sheet.Range[currentRow, 2].Text = product.Name.Length > 30 ?
                    product.Name.Substring(0, 30) + "..." : product.Name;
                sheet.Range[currentRow, 3].Text = product.Type == "Computer" ?
                    "Компьютер" : product.ComponentType ?? "Компонент";
                sheet.Range[currentRow, 4].NumberValue = product.QuantitySold;
                sheet.Range[currentRow, 5].NumberValue = (double)product.Revenue;
                sheet.Range[currentRow, 5].NumberFormat = "#,##0.00 ₽";
                sheet.Range[currentRow, 6].NumberValue = (double)product.MarketSharePercent / 100; // В процентах
                sheet.Range[currentRow, 6].NumberFormat = "0.00%";

                // Добавляем границы
                for (int col = 1; col <= 6; col++)
                {
                    sheet.Range[currentRow, col].Style.Borders.LineStyle = LineStyleType.Thin;
                    sheet.Range[currentRow, col].Style.Borders.Color = System.Drawing.Color.Black;
                }

                currentRow++;
                rank++;
            }

            // Форматирование столбцов
            sheet.AllocatedRange.AutoFitColumns();

            // Сохраняем в MemoryStream
            using var stream = new MemoryStream();
            workbook.SaveToStream(stream, FileFormat.Version2016);
            return stream.ToArray();
        }

        public async Task<MemoryStream> GenerateSalesReportAsync(StatisticsRequest request)
        {
            var stats = await GetSalesStatisticsAsync(request);
            var monthlySales = await GetMonthlySalesAsync(DateTime.Today.Year);

            // Создаем рабочую книгу
            Workbook workbook = new Workbook();
            workbook.Worksheets.Clear(); // Удаляем листы по умолчанию

            // Лист 1: Сводка
            Worksheet summarySheet = workbook.Worksheets.Add("Сводка");
            GenerateSummarySheet(summarySheet, stats);

            // Лист 2: Топ товаров
            Worksheet topProductsSheet = workbook.Worksheets.Add("Топ товаров");
            GenerateTopProductsSheet(topProductsSheet, stats);

            // Лист 3: Ежедневные продажи
            Worksheet dailySalesSheet = workbook.Worksheets.Add("Ежедневные продажи");
            GenerateDailySalesSheet(dailySalesSheet, stats);

            // Лист 4: Месячные продажи
            Worksheet monthlySalesSheet = workbook.Worksheets.Add("Месячные продажи");
            GenerateMonthlySalesSheet(monthlySalesSheet, monthlySales);

            // Лист 5: Финансовая аналитика
            Worksheet financialSheet = workbook.Worksheets.Add("Финансовая аналитика");
            await GenerateFinancialSheet(financialSheet, request);

            // Сохраняем в MemoryStream
            var stream = new MemoryStream();
            workbook.SaveToStream(stream, FileFormat.Version2016);
            stream.Position = 0;

            return stream;
        }

        private void GenerateSummarySheet(Worksheet sheet, SalesStatistics stats)
        {
            int row = 1;

            // Заголовок
            CellRange titleRange = sheet.Range[row, 1, row, 4];
            titleRange.Merge();
            titleRange.Text = "Отчет о продажах";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 16;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;
            titleRange.Style.Color = System.Drawing.Color.LightBlue;

            row += 2;

            // Период
            sheet.Range[row, 1].Text = "Период:";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 2].Text = $"{stats.PeriodStart:dd.MM.yyyy} - {stats.PeriodEnd:dd.MM.yyyy}";

            row += 2;

            // Финансовые показатели
            sheet.Range[row, 1].Text = "Финансовые показатели";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange financialHeaderRange = sheet.Range[row, 1, row, 4];
            financialHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Общая выручка:", stats.TotalRevenue, "#,##0.00 ₽",
                stats.TotalRevenue >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            AddMetric(sheet, ref row, "Себестоимость:", stats.TotalCost, "#,##0.00 ₽",
                System.Drawing.Color.Gray);

            AddMetric(sheet, ref row, "Прибыль:", stats.Profit, "#,##0.00 ₽",
                stats.Profit >= 0 ? System.Drawing.Color.DarkGreen : System.Drawing.Color.Red);

            AddMetric(sheet, ref row, "Рентабельность:", stats.ProfitMargin, "0.00 %",
                stats.ProfitMargin >= 20 ? System.Drawing.Color.Green : System.Drawing.Color.Orange);

            AddMetric(sheet, ref row, "Статус:", stats.IsProfitable ? "Прибыльный" : "Убыточный",
                stats.IsProfitable ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            row++;

            // Количественные показатели
            sheet.Range[row, 1].Text = "Количественные показатели";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange quantityHeaderRange = sheet.Range[row, 1, row, 4];
            quantityHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Всего заказов:", stats.TotalOrders, "#,##0");
            AddMetric(sheet, ref row, "Товаров продано:", stats.TotalProductsSold, "#,##0");
            AddMetric(sheet, ref row, "Компьютеров:", stats.TotalComputersSold, "#,##0");
            AddMetric(sheet, ref row, "Комплектующих:", stats.TotalComponentsSold, "#,##0");
            AddMetric(sheet, ref row, "Средний чек:", stats.AverageOrderValue, "#,##0.00 ₽");

            // Автоподбор ширины
            sheet.AllocatedRange.AutoFitColumns();
        }

        private void AddMetric(Worksheet sheet, ref int row, string label, decimal value, string? format,
                                System.Drawing.Color? color = null)
        {
            sheet.Range[row, 1].Text = label;
            sheet.Range[row, 1].Style.Font.IsBold = true;

            var valueCell = sheet.Range[row, 2];
            valueCell.NumberValue = (double)value;

            if (!string.IsNullOrEmpty(format))
            {
                valueCell.NumberFormat = format;
            }
            else
            {
                valueCell.Text = value.ToString();
            }

            if (color.HasValue)
            {
                valueCell.Style.Font.Color = color.Value;
            }

            row++;
        }

        private void AddMetric(Worksheet sheet, ref int row, string label, int value, string? format = null,
                                System.Drawing.Color? color = null)
        {
            AddMetric(sheet, ref row, label, (decimal)value, format, color);
        }

        private void AddMetric(Worksheet sheet, ref int row, string label, string value,
                                System.Drawing.Color? color = null)
        {
            sheet.Range[row, 1].Text = label;
            sheet.Range[row, 1].Style.Font.IsBold = true;

            var valueCell = sheet.Range[row, 2];
            valueCell.Text = value;

            if (color.HasValue)
            {
                valueCell.Style.Font.Color = color.Value;
            }

            row++;
        }

        private void GenerateTopProductsSheet(Worksheet sheet, SalesStatistics stats)
        {
            int row = 1;

            // Заголовок
            CellRange titleRange = sheet.Range[row, 1, row, 6];
            titleRange.Merge();
            titleRange.Text = "Топ товаров по продажам";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 14;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;
            titleRange.Style.Color = System.Drawing.Color.LightGray;

            row += 2;

            // Заголовки таблицы
            string[] headers = { "№", "Товар", "Тип", "Продано шт.", "Выручка ₽", "Доля %" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Range[row, i + 1];
                cell.Text = headers[i];
                cell.Style.Font.IsBold = true;
                cell.Style.Color = System.Drawing.Color.LightBlue;
                cell.Style.Borders.LineStyle = LineStyleType.Thin;
                cell.Style.Borders.Color = System.Drawing.Color.Black;
                cell.Style.HorizontalAlignment = HorizontalAlignType.Center;
                cell.Style.VerticalAlignment = VerticalAlignType.Center;
            }

            row++;

            // Данные
            foreach (var product in stats.TopProducts)
            {
                sheet.Range[row, 1].NumberValue = product.Rank;
                sheet.Range[row, 2].Text = product.Name.Length > 40 ?
                    product.Name.Substring(0, 40) + "..." : product.Name;
                sheet.Range[row, 3].Text = product.Type == "Computer" ?
                    "Компьютер" : product.ComponentType ?? "Компонент";
                sheet.Range[row, 4].NumberValue = product.QuantitySold;
                sheet.Range[row, 5].NumberValue = (double)product.Revenue;
                sheet.Range[row, 5].NumberFormat = "#,##0.00 ₽";
                sheet.Range[row, 6].NumberValue = (double)product.MarketSharePercent / 100;
                sheet.Range[row, 6].NumberFormat = "0.00%";

                // Добавляем границы
                for (int col = 1; col <= 6; col++)
                {
                    sheet.Range[row, col].Style.Borders.LineStyle = LineStyleType.Thin;
                    sheet.Range[row, col].Style.Borders.Color = System.Drawing.Color.Black;
                }

                // Чередование цветов строк
                if (product.Rank % 2 == 0)
                {
                    sheet.Range[row, 1, row, 6].Style.Color = System.Drawing.Color.FromArgb(240, 240, 240);
                }

                row++;
            }

            // Итоговая строка
            sheet.Range[row, 1].Text = "Итого:";
            sheet.Range[row, 1].Style.Font.IsBold = true;

            // Формулы для суммирования
            sheet.Range[row, 4].Formula = $"SUM(D3:D{row - 1})";
            sheet.Range[row, 5].Formula = $"SUM(E3:E{row - 1})";
            sheet.Range[row, 5].NumberFormat = "#,##0.00 ₽";
            sheet.Range[row, 6].Formula = $"SUM(F3:F{row - 1})";
            sheet.Range[row, 6].NumberFormat = "0.00%";

            // Стиль итоговой строки
            sheet.Range[row, 1, row, 6].Style.Color = System.Drawing.Color.LightYellow;
            sheet.Range[row, 1, row, 6].Style.Font.IsBold = true;

            // Автоподбор ширины
            sheet.AllocatedRange.AutoFitColumns();
        }

        private void GenerateDailySalesSheet(Worksheet sheet, SalesStatistics stats)
        {
            int row = 1;

            // Заголовок
            CellRange titleRange = sheet.Range[row, 1, row, 5];
            titleRange.Merge();
            titleRange.Text = "Ежедневные продажи";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 14;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;

            row += 2;

            // Заголовки таблицы
            string[] headers = { "Дата", "Заказов", "Выручка ₽", "Компьютеров", "Комплектующих" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Range[row, i + 1];
                cell.Text = headers[i];
                cell.Style.Font.IsBold = true;
                cell.Style.Color = System.Drawing.Color.LightGray;
                cell.Style.Borders.LineStyle = LineStyleType.Thin;
                cell.Style.Borders.Color = System.Drawing.Color.Black;
                cell.Style.HorizontalAlignment = HorizontalAlignType.Center;
            }

            row++;

            // Данные
            foreach (var daily in stats.DailySales)
            {
                sheet.Range[row, 1].Text = daily.Date.ToString("dd.MM.yyyy");
                sheet.Range[row, 2].NumberValue = daily.OrdersCount;
                sheet.Range[row, 3].NumberValue = (double)daily.Revenue;
                sheet.Range[row, 3].NumberFormat = "#,##0.00 ₽";
                sheet.Range[row, 4].NumberValue = daily.ComputersSold;
                sheet.Range[row, 5].NumberValue = daily.ComponentsSold;

                // Добавляем границы
                for (int col = 1; col <= 5; col++)
                {
                    sheet.Range[row, col].Style.Borders.LineStyle = LineStyleType.Thin;
                    sheet.Range[row, col].Style.Borders.Color = System.Drawing.Color.Black;
                }

                row++;
            }

            // Итоговая строка
            sheet.Range[row, 1].Text = "Итого:";
            sheet.Range[row, 1].Style.Font.IsBold = true;

            // Формулы для суммирования
            sheet.Range[row, 2].Formula = $"SUM(B3:B{row - 1})";
            sheet.Range[row, 3].Formula = $"SUM(C3:C{row - 1})";
            sheet.Range[row, 3].NumberFormat = "#,##0.00 ₽";
            sheet.Range[row, 4].Formula = $"SUM(D3:D{row - 1})";
            sheet.Range[row, 5].Formula = $"SUM(E3:E{row - 1})";

            // Стиль итоговой строки
            sheet.Range[row, 1, row, 5].Style.Color = System.Drawing.Color.LightGreen;
            sheet.Range[row, 1, row, 5].Style.Font.IsBold = true;

            // Автоподбор ширины
            sheet.AllocatedRange.AutoFitColumns();
        }

        private void GenerateMonthlySalesSheet(Worksheet sheet, List<MonthlySales> monthlySales)
        {
            int row = 1;

            // Заголовок
            CellRange titleRange = sheet.Range[row, 1, row, 7];
            titleRange.Merge();
            titleRange.Text = $"Месячные продажи за {DateTime.Today.Year} год";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 14;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;

            row += 2;

            // Заголовки таблицы
            string[] headers = { "Месяц", "Заказов", "Выручка ₽", "Себестоимость ₽", "Прибыль ₽", "Рентабельность %", "Статус" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Range[row, i + 1];
                cell.Text = headers[i];
                cell.Style.Font.IsBold = true;
                cell.Style.Color = System.Drawing.Color.LightGray;
                cell.Style.Borders.LineStyle = LineStyleType.Thin;
                cell.Style.Borders.Color = System.Drawing.Color.Black;
                cell.Style.HorizontalAlignment = HorizontalAlignType.Center;
            }

            row++;

            // Данные
            foreach (var monthly in monthlySales)
            {
                sheet.Range[row, 1].Text = monthly.MonthName;
                sheet.Range[row, 2].NumberValue = monthly.OrdersCount;
                sheet.Range[row, 3].NumberValue = (double)monthly.Revenue;
                sheet.Range[row, 3].NumberFormat = "#,##0.00 ₽";
                sheet.Range[row, 4].NumberValue = (double)monthly.Cost;
                sheet.Range[row, 4].NumberFormat = "#,##0.00 ₽";
                sheet.Range[row, 5].NumberValue = (double)monthly.Profit;
                sheet.Range[row, 5].NumberFormat = "#,##0.00 ₽";
                sheet.Range[row, 6].NumberValue = (double)monthly.ProfitMargin / 100;
                sheet.Range[row, 6].NumberFormat = "0.00%";
                sheet.Range[row, 7].Text = monthly.Profit >= 0 ? "Прибыль" : "Убыток";

                // Раскрашиваем статус
                sheet.Range[row, 7].Style.Font.Color = monthly.Profit >= 0 ?
                    System.Drawing.Color.Green : System.Drawing.Color.Red;

                // Раскрашиваем прибыль
                if (monthly.Profit > 0)
                {
                    sheet.Range[row, 5].Style.Font.Color = System.Drawing.Color.Green;
                }
                else if (monthly.Profit < 0)
                {
                    sheet.Range[row, 5].Style.Font.Color = System.Drawing.Color.Red;
                }

                // Добавляем границы
                for (int col = 1; col <= 7; col++)
                {
                    sheet.Range[row, col].Style.Borders.LineStyle = LineStyleType.Thin;
                    sheet.Range[row, col].Style.Borders.Color = System.Drawing.Color.Black;
                }

                // Чередование цветов строк
                if (monthly.Month % 2 == 0)
                {
                    sheet.Range[row, 1, row, 7].Style.Color = System.Drawing.Color.FromArgb(245, 245, 245);
                }

                row++;
            }

            // Итоговая строка
            sheet.Range[row, 1].Text = "Итого за год:";
            sheet.Range[row, 1].Style.Font.IsBold = true;

            // Формулы для суммирования
            sheet.Range[row, 2].Formula = $"SUM(B3:B{row - 1})";
            sheet.Range[row, 3].Formula = $"SUM(C3:C{row - 1})";
            sheet.Range[row, 3].NumberFormat = "#,##0.00 ₽";
            sheet.Range[row, 4].Formula = $"SUM(D3:D{row - 1})";
            sheet.Range[row, 4].NumberFormat = "#,##0.00 ₽";
            sheet.Range[row, 5].Formula = $"SUM(E3:E{row - 1})";
            sheet.Range[row, 5].NumberFormat = "#,##0.00 ₽";

            // Расчет средней рентабельности
            sheet.Range[row, 6].Formula = $"AVERAGE(F3:F{row - 1})";
            sheet.Range[row, 6].NumberFormat = "0.00%";

            // Статус года
            sheet.Range[row, 7].Formula = $"IF(E{row}>0,\"Прибыльный\",\"Убыточный\")";
            sheet.Range[row, 7].Style.Font.Color = System.Drawing.Color.Blue;

            // Стиль итоговой строки
            sheet.Range[row, 1, row, 7].Style.Color = System.Drawing.Color.LightYellow;
            sheet.Range[row, 1, row, 7].Style.Font.IsBold = true;

            // Автоподбор ширины
            sheet.AllocatedRange.AutoFitColumns();
        }

        private async Task GenerateFinancialSheet(Worksheet sheet, StatisticsRequest request)
        {
            int row = 1;

            // Заголовок
            CellRange titleRange = sheet.Range[row, 1, row, 4];
            titleRange.Merge();
            titleRange.Text = "Финансовая аналитика";
            titleRange.Style.Font.FontName = "Arial";
            titleRange.Style.Font.Size = 16;
            titleRange.Style.Font.IsBold = true;
            titleRange.Style.HorizontalAlignment = HorizontalAlignType.Center;
            titleRange.Style.Color = System.Drawing.Color.LightBlue;

            row += 2;

            // Загружаем финансовую сводку
            var financialSummary = await GetFinancialSummaryAsync();

            // Основные финансовые показатели
            sheet.Range[row, 1].Text = "Основные финансовые показатели";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange mainHeaderRange = sheet.Range[row, 1, row, 4];
            mainHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Общая выручка:", financialSummary.TotalRevenue, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Общая себестоимость:", financialSummary.TotalCost, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Общая прибыль:", financialSummary.TotalProfit, "#,##0.00 ₽",
                financialSummary.TotalProfit >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);
            AddMetric(sheet, ref row, "Общая рентабельность:", financialSummary.TotalProfitMargin, "0.00 %",
                financialSummary.TotalProfitMargin >= 20 ? System.Drawing.Color.Green : System.Drawing.Color.Orange);

            row++;

            // Текущий месяц
            sheet.Range[row, 1].Text = "Текущий месяц";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange currentMonthHeaderRange = sheet.Range[row, 1, row, 4];
            currentMonthHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Выручка:", financialSummary.ThisMonthRevenue, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Себестоимость:", financialSummary.ThisMonthCost, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Прибыль:", financialSummary.ThisMonthProfit, "#,##0.00 ₽",
                financialSummary.ThisMonthProfit >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            row++;

            // Прошлый месяц
            sheet.Range[row, 1].Text = "Прошлый месяц";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange lastMonthHeaderRange = sheet.Range[row, 1, row, 4];
            lastMonthHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Выручка:", financialSummary.LastMonthRevenue, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Себестоимость:", financialSummary.LastMonthCost, "#,##0.00 ₽");
            AddMetric(sheet, ref row, "Прибыль:", financialSummary.LastMonthProfit, "#,##0.00 ₽",
                financialSummary.LastMonthProfit >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            row++;

            // Изменения
            sheet.Range[row, 1].Text = "Изменения (текущий vs прошлый)";
            sheet.Range[row, 1].Style.Font.IsBold = true;
            sheet.Range[row, 1].Style.Font.Size = 12;
            CellRange changesHeaderRange = sheet.Range[row, 1, row, 4];
            changesHeaderRange.Merge();
            row++;

            AddMetric(sheet, ref row, "Изменение выручки:", financialSummary.RevenueChange, "#,##0.00 ₽",
                financialSummary.RevenueChange >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            AddMetric(sheet, ref row, "Процент изменения:", financialSummary.RevenueChangePercent, "0.00 %",
                financialSummary.RevenueChangePercent >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            // Автоподбор ширины
            sheet.AllocatedRange.AutoFitColumns();
        }
    }
}