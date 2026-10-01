namespace WpfClient.Models;

public sealed class CustomerListItem
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Email)
        ? FullName
        : $"{FullName} - {Email}";
}
