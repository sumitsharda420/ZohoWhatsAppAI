using Microsoft.AspNetCore.Mvc;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;

namespace ZohoWhatsAppAI.Api.Controllers;

[ApiController]
[Route("api/crm")]
[Produces("application/json")]
public class CrmController : ControllerBase
{
    private readonly ICrmSummaryService _crmSummaryService;
    private readonly ILogger<CrmController> _logger;

    public CrmController(ICrmSummaryService crmSummaryService, ILogger<CrmController> logger)
    {
        _crmSummaryService = crmSummaryService;
        _logger = logger;
    }

    /// <summary>
    /// Saves an AI-generated lead summary (and optional conversation summary) as a Zoho CRM Note.
    /// </summary>
    [HttpPost("save-summary/{leadId}")]
    [ProducesResponseType(typeof(SaveSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SaveSummaryResponse>> SaveSummary(
        string leadId,
        [FromBody] SaveSummaryRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Save summary requested for lead {LeadId}.", leadId);
            var result = await _crmSummaryService.SaveSummaryAsync(leadId, request, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(CreateProblem("Lead Not Found", ex.Message, StatusCodes.Status404NotFound));
        }
        catch (ConfigurationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CreateProblem("Configuration Error", ex.Message, StatusCodes.Status500InternalServerError));
        }
        catch (ExternalServiceException ex)
        {
            _logger.LogError(ex, "External service error during save-summary for {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, CreateProblem("External Service Error", ex.Message, ex.StatusCode));
        }
    }

    private static ProblemDetails CreateProblem(string title, string detail, int status) =>
        new() { Title = title, Detail = detail, Status = status };
}
