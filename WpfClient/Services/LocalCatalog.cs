using WpfClient.Models;

namespace WpfClient.Services;

public static class LocalCatalog
{
    public static IReadOnlyList<ProductListItem> CreateProducts()
    {
        return
        [
            new() { Id = 1, Name = "Notebook A5", Category = "Office supplies", Price = 45m, Quantity = 120, IsActive = true },
            new() { Id = 2, Name = "Blue pen", Category = "Office supplies", Price = 12.5m, Quantity = 300, IsActive = true },
            new() { Id = 3, Name = "Folder", Category = "Office supplies", Price = 18m, Quantity = 80, IsActive = true },
            new() { Id = 4, Name = "USB flash drive 32GB", Category = "Electronics", Price = 220m, Quantity = 35, IsActive = true },
            new() { Id = 5, Name = "Wireless mouse", Category = "Electronics", Price = 420m, Quantity = 25, IsActive = true },
            new() { Id = 6, Name = "Office chair", Category = "Furniture", Price = 2400m, Quantity = 10, IsActive = true }
        ];
    }

    public static IReadOnlyList<CustomerListItem> CreateCustomers()
    {
        return
        [
            new() { Id = 1, FullName = "Demo Customer", Phone = "+380000000000", Email = "customer@example.com" },
            new() { Id = 2, FullName = "Lviv Office" },
            new() { Id = 3, FullName = "Student Lab" },
            new() { Id = 4, FullName = "Library" },
            new() { Id = 5, FullName = "Dean Office" }
        ];
    }

    public static string GetCustomerName(int customerId)
    {
        return CreateCustomers().FirstOrDefault(customer => customer.Id == customerId)?.FullName
            ?? $"Customer #{customerId}";
    }
}
