using System.ComponentModel.DataAnnotations;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string TaxId { get; init; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "CountryCode must be a two-letter ISO code.")]
    public string CountryCode { get; init; } = string.Empty;

    [Required, EnumDataType(typeof(CompanyType))]
    public CompanyType? CompanyType { get; init; }
}
