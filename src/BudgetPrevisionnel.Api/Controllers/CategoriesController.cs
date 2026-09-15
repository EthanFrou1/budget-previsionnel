using BudgetPrevisionnel.Api.Contracts.Categories;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController(CategoryService categoryService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetVisibleToUserAsync(currentUser.UserId, cancellationToken);
        return Ok(categories.Select(c => CategoryResponse.FromEntity(c, currentUser.UserId)));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(
            currentUser.UserId, request.Name, request.Icon, request.Color, request.ParentCategoryId, cancellationToken);
        return Ok(CategoryResponse.FromEntity(category, currentUser.UserId));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.UpdateAsync(
            currentUser.UserId, id, request.Name, request.Icon, request.Color, request.ParentCategoryId, cancellationToken);
        return Ok(CategoryResponse.FromEntity(category, currentUser.UserId));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
