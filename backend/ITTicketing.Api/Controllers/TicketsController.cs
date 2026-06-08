using ITTicketing.Api.Models;
using ITTicketing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TicketsController(TicketStore ticketStore) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Ticket>> Get(
        [FromQuery] BranchLocation? branch,
        [FromQuery] TicketPriority? priority,
        [FromQuery] TicketStatus? status)
    {
        return Ok(ticketStore.GetTickets(branch, priority, status));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<Ticket> GetById(Guid id)
    {
        var ticket = ticketStore.GetTicket(id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationProblem("Title and description are required.");
        }

        var created = await ticketStore.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<Ticket>> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var updated = await ticketStore.UpdateStatusAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/details")]
    public async Task<ActionResult<Ticket>> UpdateDetails(Guid id, [FromBody] UpdateTicketDetailsRequest request, CancellationToken cancellationToken)
    {
        var updated = await ticketStore.UpdateDetailsAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("summary")]
    public ActionResult<TicketSummaryResponse> GetSummary()
    {
        return Ok(ticketStore.GetSummary());
    }
}
