using Card.Application.DTOs;
using Card.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Card.Api.Controllers;

[ApiController]
[Route("api/v1/cartoes")]
public class CardController : ControllerBase
{
    private readonly ICardService _service;

    public CardController(ICardService service)
    {
        _service = service;
    }

    [HttpGet("{clienteId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CardResponse>>> GetByClienteIdAsync(
        [FromRoute] Guid clienteId, CancellationToken cancellationToken)
    {
        var cards = await _service.GetByClienteIdAsync(clienteId, cancellationToken);
        if (cards.Count == 0)
            return NotFound();

        return Ok(cards);
    }
}
