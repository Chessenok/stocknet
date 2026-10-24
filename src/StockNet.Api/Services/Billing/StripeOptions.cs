using StockNet.Api.Options;

namespace StockNet.Api.Services.Billing;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PriceId { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;

    public void ValidateApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new StripeBillingConfigurationException("Stripe API access is not configured.");
        }
    }

    public void ValidateCheckoutConfiguration()
    {
        ValidateApiKey();
        if (string.IsNullOrWhiteSpace(PriceId)
            || string.IsNullOrWhiteSpace(SuccessUrl)
            || string.IsNullOrWhiteSpace(CancelUrl))
        {
            throw new StripeBillingConfigurationException("Stripe checkout is not configured.");
        }
    }

    public void ValidateWebhookConfiguration()
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            throw new StripeBillingConfigurationException("Stripe webhook signing is not configured.");
        }
    }
}
