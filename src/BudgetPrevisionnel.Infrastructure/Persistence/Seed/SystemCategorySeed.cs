using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Seed;

/// <summary>
/// Baseline system categories, seeded via EF Core migrations (HasData) so every
/// environment starts with the same reference data. Ids are pinned explicitly:
/// HasData requires stable keys, and future additions must append rather than
/// renumber to avoid rewriting existing users' CategoryId references.
/// </summary>
internal static class SystemCategorySeed
{
    public static IReadOnlyList<Category> Categories { get; } =
    [
        new Category { Id = 1, Name = "Alimentation", Icon = "utensils", Color = "#F97316", IsSystemDefault = true },
        new Category { Id = 2, Name = "Logement", Icon = "home", Color = "#0EA5E9", IsSystemDefault = true },
        new Category { Id = 3, Name = "Transport", Icon = "car", Color = "#6366F1", IsSystemDefault = true },
        new Category { Id = 4, Name = "Abonnements & téléphonie", Icon = "credit-card", Color = "#8B5CF6", IsSystemDefault = true },
        new Category { Id = 5, Name = "Loisirs", Icon = "gamepad-2", Color = "#EC4899", IsSystemDefault = true },
        new Category { Id = 6, Name = "Santé", Icon = "heart-pulse", Color = "#EF4444", IsSystemDefault = true },
        new Category { Id = 7, Name = "Revenus", Icon = "trending-up", Color = "#22C55E", IsSystemDefault = true },
        new Category { Id = 8, Name = "Épargne", Icon = "piggy-bank", Color = "#14B8A6", IsSystemDefault = true },
        new Category { Id = 9, Name = "Impôts & taxes", Icon = "receipt", Color = "#78716C", IsSystemDefault = true },
        new Category { Id = 10, Name = "Autres", Icon = "ellipsis", Color = "#94A3B8", IsSystemDefault = true },
    ];
}
