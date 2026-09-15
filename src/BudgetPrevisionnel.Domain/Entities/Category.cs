namespace BudgetPrevisionnel.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }

    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    public bool IsSystemDefault { get; set; }

    // Null for system categories; set for a user's own custom category.
    // Custom categories are never shared between users.
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
}
