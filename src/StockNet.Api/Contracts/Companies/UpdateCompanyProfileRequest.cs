using System.ComponentModel.DataAnnotations;
using StockNet.Api.Domain.Enums;

namespace StockNet.Api.Contracts.Companies;

public sealed class UpdateCompanyProfileRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(64)]
    public string? TaxId { get; init; }

    [Required, RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "CountryCode must be a two-letter ISO code.")]
    public string CountryCode { get; init; } = string.Empty;

    [Required, EnumDataType(typeof(CompanyType))]
    public CompanyType? CompanyType { get; init; }
}
