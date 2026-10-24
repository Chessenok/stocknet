using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using StockNet.Api.Contracts.Billing;
using StockNet.Api.Infrastructure.Authorization;
using StockNet.Api.Options;
using StockNet.Api.Services.Billing;

namespace StockNet.Api.Controllers;

[ApiController]
[Route("api/billing")]
public sealed class BillingController(
    IStripeBillingService billingService,
    IOptions<StripeOptions> stripeOptions) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout-session")]
    [ProducesResponseType<CheckoutSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckoutSessionDto>> CreateCheckoutSession(CancellationToken cancellationToken)
    {
        var companyId = User.GetCompanyId();
        try
        {
            var session = await billingService.CreateCheckoutSessionAsync(companyId, cancellationToken);
            return session is null ? NotFound() : Ok(session);
        }
        catch (StripeBillingConfigurationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Stripe billing is not configured."
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("webhooks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        try
        {
            stripeOptions.Value.ValidateWebhookConfiguration();
        }
        catch (StripeBillingConfigurationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Stripe webhook processing is not configured."
            });
        }

        var webhookSecret = stripeOptions.Value.WebhookSecret;

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(InvalidWebhookSignature());
        }

        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        Event stripeEvent;
        try
        {
            // Stripe.net 53.0.0 exposes the signature-verifying ConstructEvent API synchronously.
            // The request body and all subsequent database/network I/O remain asynchronous.
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return BadRequest(InvalidWebhookSignature());
        }
        catch (JsonException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Stripe webhook payload."
            });
        }

        var processed = await billingService.ProcessWebhookEventAsync(stripeEvent, cancellationToken);
        return Ok(new { received = true, duplicate = !processed });
    }

    private static ProblemDetails InvalidWebhookSignature() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid Stripe webhook signature."
    };
}
