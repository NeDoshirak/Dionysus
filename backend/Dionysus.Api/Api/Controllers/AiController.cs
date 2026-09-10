using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(ITextGenerationService ai) : ControllerBase
{
    [HttpPost("respond")]
    public async Task<IActionResult> Respond(YandexAiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return BadRequest(new { detail = "Input is required" });

        try
        {
            var result = await ai.RespondAsync(request.Input, request.Instructions, cancellationToken);
            return Ok(new YandexAiResponseDto(result.ResponseId, result.Model, result.Text, result.TotalTokens));
        }
        catch (YandexAiException exception)
        {
            var problem = new ProblemDetails
            {
                Title = "Yandex AI request failed",
                Status = StatusCodes.Status502BadGateway
            };
            problem.Extensions["providerStatus"] = exception.StatusCode;
            return StatusCode(StatusCodes.Status502BadGateway, problem);
        }
        catch (HttpRequestException)
        {
            return Problem("Yandex AI is unavailable", statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException exception) when (exception.Message == "Yandex AI is not configured")
        {
            return Problem("Yandex AI is not configured", statusCode: 503);
        }
    }
}
