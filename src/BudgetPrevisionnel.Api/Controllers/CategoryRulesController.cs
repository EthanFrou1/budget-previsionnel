using BudgetPrevisionnel.Api.Contracts.Categories;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/category-rules")]
[Authorize]
public class CategoryRulesController(CategoryRuleService ruleService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryRuleResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var rules = await ruleService.GetForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(rules.Select(CategoryRuleResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryRuleResponse>> Create(CreateCategoryRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await ruleService.CreateAsync(
            currentUser.UserId, request.MatchPattern, request.CategoryId, request.Priority, cancellationToken);
        return Ok(CategoryRuleResponse.FromEntity(rule));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await ruleService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
