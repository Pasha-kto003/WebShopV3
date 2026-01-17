// Models/Statistics/StatisticsModels.cs
namespace WebShopV3.Json.Models.Statistics
{
    public class SalesStatistics
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Финансовые показатели
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit => TotalRevenue - TotalCost;
        public decimal ProfitMargin => TotalRevenue > 0 ? (Profit / TotalRevenue) * 100 : 0;
        public bool IsProfitable => Profit > 0;

        // Количественные показатели
        public int TotalOrders { get; set; }
        public int TotalProductsSold { get; set; }
        public int TotalComputersSold { get; set; }
        public int TotalComponentsSold { get; set; }

        // Средние значения
        public decimal AverageOrderValue => TotalOrders > 0 ? TotalRevenue / TotalOrders : 0;
        public decimal AverageProductPrice => TotalProductsSold > 0 ? TotalRevenue / TotalProductsSold : 0;

        // Динамика
        public decimal RevenueChangePercent { get; set; } // Изменение выручки к предыдущему периоду
        public int OrdersChangePercent { get; set; } // Изменение количества заказов

        // Топ товаров
        public List<TopProduct> TopComputers { get; set; } = new();
        public List<TopProduct> TopComponents { get; set; } = new();
        public List<TopProduct> TopProducts { get; set; } = new();

        // Распределение по типам заказов
        public Dictionary<string, int> OrderTypeDistribution { get; set; } = new();

        // По дням/неделям/месяцам
        public List<DailySales> DailySales { get; set; } = new();
        public List<MonthlySales> MonthlySales { get; set; } = new();
    }

    public class TopProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Computer" или "Component"
        public string? ComponentType { get; set; } // Для компонентов: CPU, GPU и т.д.
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal AveragePrice => QuantitySold > 0 ? Revenue / QuantitySold : 0;
        public int Rank { get; set; }
        public decimal MarketSharePercent { get; set; } // Доля в общем объеме продаж
    }

    public class DailySales
    {
        public DateTime Date { get; set; }
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
        public int ProductsSold { get; set; }
        public int ComputersSold { get; set; }
        public int ComponentsSold { get; set; }
    }

    public class MonthlySales
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
        public decimal ProfitMargin => Revenue > 0 ? (Profit / Revenue) * 100 : 0;
        public string Status => Profit >= 0 ? "Прибыль" : "Убыток";
        public System.Drawing.Color StatusColor => Profit >= 0 ?
            System.Drawing.Color.Green : System.Drawing.Color.Red;

        public decimal ProfitValue => Profit;
        public decimal ProfitMarginValue => ProfitMargin;
    }

    public class FinancialSummary
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => TotalRevenue - TotalCost;
        public decimal TotalProfitMargin => TotalRevenue > 0 ? (TotalProfit / TotalRevenue) * 100 : 0;

        public decimal ThisMonthRevenue { get; set; }
        public decimal ThisMonthCost { get; set; }
        public decimal ThisMonthProfit => ThisMonthRevenue - ThisMonthCost;

        public decimal LastMonthRevenue { get; set; }
        public decimal LastMonthCost { get; set; }
        public decimal LastMonthProfit => LastMonthRevenue - LastMonthCost;

        public decimal RevenueChange => ThisMonthRevenue - LastMonthRevenue;
        public decimal RevenueChangePercent => LastMonthRevenue > 0
            ? (RevenueChange / LastMonthRevenue) * 100
            : (ThisMonthRevenue > 0 ? 100 : 0);

        public bool IsProfitable => TotalProfit > 0;
        public string ProfitStatus => IsProfitable ? "Прибыльный" : "Убыточный";
        public string ProfitStatusClass => IsProfitable ? "profit-positive" : "profit-negative";
    }

    public class StatisticsRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PeriodType { get; set; } = "month"; // day, week, month, quarter, year, custom
        public int? TopProductsCount { get; set; } = 10;
        public bool IncludeDetails { get; set; } = true;
        public string? ProductType { get; set; } // "all", "computers", "components"
        public string? ComponentCategory { get; set; } // Для фильтрации компонентов
    }

    public class ExportRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ExportType { get; set; } = "excel"; // excel, pdf, csv
        public string ReportType { get; set; } = "sales"; // sales, products, financial
        public bool IncludeCharts { get; set; } = true;
    }
}