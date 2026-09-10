using SerialManager.Models;

namespace SerialManager.Helpers;

internal static class ArticleGroupingHelper
{
    public const string AllCustomers = "Alle";

    public static List<string> GetCustomerNumbers(IEnumerable<Article> articles)
    {
        var customers = articles
            .Select(a => a.CustomerNumber)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        customers.Insert(0, AllCustomers);
        return customers;
    }

    public static List<Article> FilterByCustomer(IEnumerable<Article> articles, string? customer)
    {
        if (string.IsNullOrWhiteSpace(customer) || customer == AllCustomers)
            return articles.OrderBy(a => a.ArticleNumber).ToList();

        return articles
            .Where(a => a.CustomerNumber == customer)
            .OrderBy(a => a.ArticleNumber)
            .ToList();
    }
}