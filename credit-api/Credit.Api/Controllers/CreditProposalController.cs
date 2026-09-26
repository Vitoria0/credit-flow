using Credit.Application.DTOs;
using Credit.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Credit.Api.Controllers;

[ApiController]
[Route("api/v1/propostas")]
public class CreditProposalController : ControllerBase
{
    private readonly ICreditProposalService _service;

    public CreditProposalController(ICreditProposalService service)
    {
        _service = service;
    }

    [HttpGet("{clienteId:guid}")]
    [ProducesResponseType(typeof(CreditProposalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditProposalResponse>> GetByClienteIdAsync(
        [FromRoute] Guid clienteId, CancellationToken cancellationToken)
    {
        var proposal = await _service.GetByClienteIdAsync(clienteId, cancellationToken);

        if (proposal is null)
        {
            return NotFound();
        }

        return Ok(proposal);
    }
}
