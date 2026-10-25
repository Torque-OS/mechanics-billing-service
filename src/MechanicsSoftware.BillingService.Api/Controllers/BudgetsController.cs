using System.IdentityModel.Tokens.Jwt;
using MechanicsSoftware.BillingService.Api.Transport.Budgets;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Queries;
using MechanicsSoftware.BillingService.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicsSoftware.BillingService.Api.Controllers;

[ApiController]
[Route("api/budgets")]
[Authorize(Policy = Policies.CustomerOrStaff)]
public class BudgetsController(
    GenerateBudgetHandler generateHandler,
    DecideBudgetHandler decideHandler,
    GetBudgetHandler getHandler,
    GetBudgetByServiceOrderHandler getByServiceOrderHandler) : ControllerBase
{
    /// <summary>
    /// Generates the budget of a service order from its item snapshot. Idempotent per service order:
    /// 201 when created, 200 with the existing budget when it already exists.
    /// The Saga consumer of <c>RequestBudget</c> (F4-13) calls the same handler.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<BudgetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<BudgetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Generate(GenerateBudgetRequest request, CancellationToken cancellationToken)
    {
        var result = await generateHandler.ExecuteAsync(request.ToCommand(), cancellationToken);

        if (result.Failed)
            return UnprocessableEntity(new { error = result.FailureReason });

        return result.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Budget!.Id }, result.Budget)
            : Ok(result.Budget);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<BudgetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await getHandler.ExecuteAsync(id, cancellationToken));

    [HttpGet]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<BudgetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByServiceOrder([FromQuery] Guid serviceOrderId, CancellationToken cancellationToken)
    {
        if (serviceOrderId == Guid.Empty)
            return BadRequest(new { error = "Query parameter 'serviceOrderId' is required." });

        return Ok(await getByServiceOrderHandler.ExecuteAsync(
            new GetBudgetByServiceOrderQuery(serviceOrderId), cancellationToken));
    }

    /// <summary>Approves or rejects a budget. Customers may only decide their own budgets.</summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<BudgetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(Guid id, BudgetDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return Forbid();

        var requester = User.IsInRole(Policies.CustomerRole)
            ? BudgetRequester.Customer(userId)
            : BudgetRequester.Staff(userId);

        return Ok(await decideHandler.ExecuteAsync(
            new DecideBudgetCommand(id, request.Decision, requester), cancellationToken));
    }
}
