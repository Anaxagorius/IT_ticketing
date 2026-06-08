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
    public ActionResult<Ticket> Create([FromBody] CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationProblem("Title and description are required.");
        }

        var created = ticketStore.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/status")]
    public ActionResult<Ticket> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusRequest request)
    {
        var updated = ticketStore.UpdateStatus(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("summary")]
    public ActionResult<TicketSummaryResponse> GetSummary()
    {
        return Ok(ticketStore.GetSummary());
    }
}
