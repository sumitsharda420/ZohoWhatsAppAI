using Microsoft.AspNetCore.Mvc;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;

namespace ZohoWhatsAppAI.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Produces("application/json")]
public class AIController : ControllerBase
{
    private readonly ILeadAnalysisService _leadAnalysisService;
    private readonly ILogger<AIController> _logger;

    public AIController(ILeadAnalysisService leadAnalysisService, ILogger<AIController> logger)
    {
        _leadAnalysisService = leadAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a lead from Zoho CRM and returns AI-generated summary, score, and recommended action.
    /// </summary>
    [HttpPost("analyze-lead/{leadId}")]
    [ProducesResponseType(typeof(AnalyzeLeadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<AnalyzeLeadResponse>> AnalyzeLead(
        string leadId,
        [FromBody] AnalyzeLeadRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Analyze lead requested for {LeadId}.", leadId);
            var result = await _leadAnalysisService.AnalyzeLeadAsync(leadId, request, cancellationToken);
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
            _logger.LogError(ex, "External service error during analyze-lead for {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, CreateProblem("External Service Error", ex.Message, ex.StatusCode));
        }
    }

    private static ProblemDetails CreateProblem(string title, string detail, int status) =>
        new() { Title = title, Detail = detail, Status = status };
}
