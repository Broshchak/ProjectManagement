using System.ComponentModel.DataAnnotations;

namespace Application.Dtos.Customers;

public sealed record CustomerDto(int Id, string FullName, string? Phone, string? Email, string? Address);

public sealed record SaveCustomerDto(
    [Required, StringLength(150)] string FullName,
    [StringLength(40)] string? Phone,
    [EmailAddress, StringLength(160)] string? Email,
    string? Address);
