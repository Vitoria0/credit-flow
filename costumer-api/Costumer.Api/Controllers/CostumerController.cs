using Costumer.Application.DTOs;
using Costumer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Costumer.Api.Controllers;

[ApiController]
[Route("api/v1/clientes")]
public class CostumerController : ControllerBase
{
    private readonly ICostumerService _service;

    public CostumerController(ICostumerService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CostumerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CostumerResponse>> CreateAsync(
        [FromBody] CreateCostumerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var costumer = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = costumer.Id }, costumer);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CostumerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CostumerResponse>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var costumer = await _service.GetByIdAsync(id, cancellationToken);

        if (costumer is null)
        {
            return NotFound();
        }

        return Ok(costumer);
    }
}
