using Microsoft.AspNetCore.Mvc;

namespace MechanicsSoftware.BillingService.Api.Controllers;

// Placeholder for the Billing Service's own endpoints (budget generation/decision, payment
// webhooks — F4-14/F4-15). Domain, Application and Infrastructure layers are added there,
// mirroring the layout of mechanics-software (ADR-004).
[ApiController]
[Route("api/budgets")]
public class BudgetsController : ControllerBase
{
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) =>
        StatusCode(StatusCodes.Status501NotImplemented, new { message = "Not implemented yet — see F4-14." });
}
