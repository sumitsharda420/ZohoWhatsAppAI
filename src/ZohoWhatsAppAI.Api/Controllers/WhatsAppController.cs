using Microsoft.AspNetCore.Mvc;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Application.DTOs;
using ZohoWhatsAppAI.Application.Interfaces;

namespace ZohoWhatsAppAI.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Produces("application/json")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(IWhatsAppService whatsAppService, ILogger<WhatsAppController> logger)
    {
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a personalized WhatsApp outreach message for a Zoho CRM lead.
    /// </summary>
    [HttpPost("generate-message/{leadId}")]
    [ProducesResponseType(typeof(GenerateWhatsAppMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GenerateWhatsAppMessageResponse>> GenerateMessage(
        string leadId,
        [FromBody] GenerateWhatsAppMessageRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generate WhatsApp message requested for {LeadId}.", leadId);
            var result = await _whatsAppService.GenerateMessageAsync(leadId, request, cancellationToken);
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
            _logger.LogError(ex, "External service error during generate-message for {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, CreateProblem("External Service Error", ex.Message, ex.StatusCode));
        }
    }

    /// <summary>
    /// Builds a WhatsApp Business API message template using AI.
    /// </summary>
    [HttpPost("build-template")]
    [ProducesResponseType(typeof(BuildWhatsAppTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BuildWhatsAppTemplateResponse>> BuildTemplate(
        [FromBody] BuildWhatsAppTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _whatsAppService.BuildTemplateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ConfigurationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CreateProblem("Configuration Error", ex.Message, StatusCodes.Status500InternalServerError));
        }
        catch (ExternalServiceException ex)
        {
            return StatusCode(ex.StatusCode, CreateProblem("External Service Error", ex.Message, ex.StatusCode));
        }
    }

    /// <summary>
    /// Summarizes a WhatsApp conversation transcript for CRM follow-up.
    /// </summary>
    [HttpPost("conversation-summary/{leadId}")]
    [ProducesResponseType(typeof(ConversationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationSummaryResponse>> ConversationSummary(
        string leadId,
        [FromBody] ConversationSummaryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _whatsAppService.GenerateConversationSummaryAsync(
                leadId,
                request,
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateProblem("Invalid Request", ex.Message, StatusCodes.Status400BadRequest));
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
            return StatusCode(ex.StatusCode, CreateProblem("External Service Error", ex.Message, ex.StatusCode));
        }
    }

    private static ProblemDetails CreateProblem(string title, string detail, int status) =>
        new() { Title = title, Detail = detail, Status = status };
}
