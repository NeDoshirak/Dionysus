using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/specification")]
public sealed class SpecificationsController(AppDbContext db, ISpecificationAnalysisQueue queue) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        var analysis = await LoadAnalysis(projectId, ct);
        return analysis is null ? NotFound() : Ok(SpecificationMapper.ToDetails(analysis));
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry(Guid projectId, CancellationToken ct)
    {
        var analysis = await LoadAnalysis(projectId, ct);
        if (analysis is null) return NotFound();
        if (analysis.Status != SpecificationAnalysisStatus.Failed)
            return Conflict(new { detail = "Only failed analyses can be retried." });

        db.AnalysisTopics.RemoveRange(analysis.Topics);
        db.AnalysisStatements.RemoveRange(analysis.Statements);
        db.AnalysisRelations.RemoveRange(analysis.Relations);
        db.SpecificationItems.RemoveRange(analysis.Items);
        db.SpecificationFunctions.RemoveRange(analysis.Functions);
        analysis.RunId = Guid.NewGuid();
        analysis.RetryCount++;
        analysis.Status = SpecificationAnalysisStatus.Queued;
        analysis.Error = null;
        analysis.CompletedAt = null;
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), ct);
        return Accepted(new { analysisId = analysis.Id, runId = analysis.RunId });
    }

    [HttpGet("functions/{functionId:guid}")]
    public async Task<IActionResult> GetFunction(Guid projectId, Guid functionId, CancellationToken ct)
    {
        var function = await LoadFunction(projectId, functionId, ct);
        return function is null ? NotFound() : Ok(SpecificationMapper.ToFunction(function));
    }

    [HttpPost("functions")]
    public async Task<IActionResult> CreateFunction(Guid projectId, CreateSpecificationFunctionRequest request, CancellationToken ct)
    {
        var analysis = await LoadAnalysis(projectId, ct);
        if (analysis is null) return NotFound();
        if (analysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        if (!await ValidSources(analysis.Id, request.SourceStatementIds, ct)) return BadRequest(new { detail = "Unknown source statement." });

        var function = new SpecificationFunction
        {
            SpecificationAnalysisId = analysis.Id,
            Title = request.Title,
            Description = request.Description,
            SortOrder = request.SortOrder,
            StatementLinks = request.SourceStatementIds.Select(id => new SpecificationFunctionStatement { AnalysisStatementId = id }).ToList()
        };
        db.SpecificationFunctions.Add(function);
        await db.SaveChangesAsync(ct);
        await LoadFunction(projectId, function.Id, ct);
        return CreatedAtAction(nameof(GetFunction), new { projectId, functionId = function.Id }, SpecificationMapper.ToFunction(function));
    }

    [HttpPatch("functions/{functionId:guid}")]
    public async Task<IActionResult> UpdateFunction(Guid projectId, Guid functionId, UpdateSpecificationFunctionRequest request, CancellationToken ct)
    {
        var function = await LoadFunction(projectId, functionId, ct);
        if (function is null) return NotFound();
        if (function.SpecificationAnalysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        if (request.SourceStatementIds is not null && !await ValidSources(function.SpecificationAnalysisId, request.SourceStatementIds, ct))
            return BadRequest(new { detail = "Unknown source statement." });

        function.Title = request.Title;
        function.Description = request.Description;
        function.SortOrder = request.SortOrder;
        if (request.SourceStatementIds is not null)
        {
            ReplaceFunctionSources(function, request.SourceStatementIds);
        }
        await db.SaveChangesAsync(ct);
        return Ok(SpecificationMapper.ToFunction(function));
    }

    [HttpDelete("functions/{functionId:guid}")]
    public async Task<IActionResult> DeleteFunction(Guid projectId, Guid functionId, CancellationToken ct)
    {
        var function = await LoadFunction(projectId, functionId, ct);
        if (function is null) return NotFound();
        if (function.SpecificationAnalysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        db.SpecificationFunctions.Remove(function);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("functions/{functionId:guid}/items")]
    public async Task<IActionResult> CreateItem(Guid projectId, Guid functionId, CreateSpecificationItemRequest request, CancellationToken ct)
    {
        var function = await LoadFunction(projectId, functionId, ct);
        if (function is null) return NotFound();
        if (function.SpecificationAnalysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        if (!await ValidSources(function.SpecificationAnalysisId, request.SourceStatementIds, ct)) return BadRequest(new { detail = "Unknown source statement." });

        var item = new SpecificationItem
        {
            SpecificationAnalysisId = function.SpecificationAnalysisId,
            SpecificationFunctionId = function.Id,
            Kind = request.Kind,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            IsManual = true,
            StatementLinks = request.SourceStatementIds.Select(id => new SpecificationItemStatement { AnalysisStatementId = id }).ToList()
        };
        db.SpecificationItems.Add(item);
        await db.SaveChangesAsync(ct);
        await LoadItem(projectId, functionId, item.Id, ct);
        return CreatedAtAction(nameof(GetFunction), new { projectId, functionId }, SpecificationMapper.ToItem(item));
    }

    [HttpPatch("functions/{functionId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid projectId, Guid functionId, Guid itemId, UpdateSpecificationItemRequest request, CancellationToken ct)
    {
        var item = await LoadItem(projectId, functionId, itemId, ct);
        if (item is null) return NotFound();
        if (item.SpecificationAnalysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        if (request.SourceStatementIds is not null && !await ValidSources(item.SpecificationAnalysisId, request.SourceStatementIds, ct))
            return BadRequest(new { detail = "Unknown source statement." });

        item.Title = request.Title;
        item.Description = request.Description;
        item.Priority = request.Priority;
        if (request.SourceStatementIds is not null)
        {
            ReplaceItemSources(item, request.SourceStatementIds);
        }
        await db.SaveChangesAsync(ct);
        return Ok(SpecificationMapper.ToItem(item));
    }

    [HttpDelete("functions/{functionId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid projectId, Guid functionId, Guid itemId, CancellationToken ct)
    {
        var item = await LoadItem(projectId, functionId, itemId, ct);
        if (item is null) return NotFound();
        if (item.SpecificationAnalysis.Status != SpecificationAnalysisStatus.Completed) return Conflict();
        db.SpecificationItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private Task<SpecificationAnalysis?> LoadAnalysis(Guid projectId, CancellationToken ct) => db.SpecificationAnalyses
        .Where(x => x.ProjectEntityId == projectId && x.Project.OwnerId == UserId)
        .Include(x => x.Items).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
        .Include(x => x.Functions).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement)
        .Include(x => x.Functions).ThenInclude(x => x.Items).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
        .FirstOrDefaultAsync(ct);

    private Task<SpecificationFunction?> LoadFunction(Guid projectId, Guid functionId, CancellationToken ct) => db.SpecificationFunctions
        .Where(x => x.Id == functionId && x.SpecificationAnalysis.ProjectEntityId == projectId && x.SpecificationAnalysis.Project.OwnerId == UserId)
        .Include(x => x.SpecificationAnalysis)
        .Include(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement)
        .Include(x => x.Items).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
        .FirstOrDefaultAsync(ct);

    private Task<SpecificationItem?> LoadItem(Guid projectId, Guid functionId, Guid itemId, CancellationToken ct) => db.SpecificationItems
        .Where(x => x.Id == itemId && x.SpecificationFunctionId == functionId && x.SpecificationFunction!.SpecificationAnalysis.ProjectEntityId == projectId && x.SpecificationFunction.SpecificationAnalysis.Project.OwnerId == UserId)
        .Include(x => x.SpecificationAnalysis)
        .Include(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
        .FirstOrDefaultAsync(ct);

    private async Task<bool> ValidSources(Guid analysisId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var unique = ids.Distinct().ToArray();
        return unique.Length == ids.Count && await db.AnalysisStatements.CountAsync(x => x.SpecificationAnalysisId == analysisId && unique.Contains(x.Id), ct) == unique.Length;
    }

    private void ReplaceFunctionSources(SpecificationFunction function, IReadOnlyList<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        db.SpecificationFunctionStatements.RemoveRange(function.StatementLinks.Where(x => !wanted.Contains(x.AnalysisStatementId)));
        foreach (var id in wanted.Except(function.StatementLinks.Select(x => x.AnalysisStatementId)))
            function.StatementLinks.Add(new SpecificationFunctionStatement { SpecificationFunctionId = function.Id, AnalysisStatementId = id });
    }

    private void ReplaceItemSources(SpecificationItem item, IReadOnlyList<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        db.SpecificationItemStatements.RemoveRange(item.StatementLinks.Where(x => !wanted.Contains(x.AnalysisStatementId)));
        foreach (var id in wanted.Except(item.StatementLinks.Select(x => x.AnalysisStatementId)))
            item.StatementLinks.Add(new SpecificationItemStatement { SpecificationItemId = item.Id, AnalysisStatementId = id });
    }
}
