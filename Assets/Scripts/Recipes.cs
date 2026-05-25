using System.Collections.Generic;

public enum Ingredient
{
    Pepper,
    Tomato,
    Carrot,
    GoldenEgg,
    WerewolfSteak,
    WerewolfTail,
    YetiRibs,
    YetiDrumstick
}

public enum Recipe
{
    PepperSteak,
    WerewolfTailSoup,
    YetiBBQPlate,
    Quackshuka,
    NashvilleHotYeti,
    Quacklet
}

public static class Recipes
{
    public static readonly Dictionary<Recipe, List<Ingredient>> RecipeIngredients = new()
    {
        { Recipe.PepperSteak, new List<Ingredient> { Ingredient.WerewolfSteak, Ingredient.Pepper } },
        { Recipe.WerewolfTailSoup, new List<Ingredient> { Ingredient.Tomato, Ingredient.WerewolfTail } },
        { Recipe.YetiBBQPlate, new List<Ingredient> { Ingredient.YetiRibs, Ingredient.Carrot } },
        { Recipe.Quackshuka, new List<Ingredient> { Ingredient.GoldenEgg, Ingredient.Tomato } },
        { Recipe.NashvilleHotYeti, new List<Ingredient> { Ingredient.YetiDrumstick, Ingredient.Carrot } },
        { Recipe.Quacklet, new List<Ingredient> { Ingredient.WerewolfSteak, Ingredient.GoldenEgg } }
    };

    public static List<Ingredient> GetIngredientsForRecipe(Recipe recipe)
    {
        if (RecipeIngredients.TryGetValue(recipe, out List<Ingredient> ingredients))
        {
            return ingredients;
        }
        return new List<Ingredient>();
    }

    public static Ingredient GetProteinForRecipe(Recipe recipe)
    {
        List<Ingredient> ingredients = GetIngredientsForRecipe(recipe);
        foreach (Ingredient ingredient in ingredients)
        {
            if (IsProtein(ingredient))
            {
                return ingredient;
            }
        }
        return default; // or throw an exception if you prefer
    }

    public static Ingredient GetVegetableForRecipe(Recipe recipe)
    {
        List<Ingredient> ingredients = GetIngredientsForRecipe(recipe);
        foreach (Ingredient ingredient in ingredients)
        {
            if (IsVegetable(ingredient))
            {
                return ingredient;
            }
        }
        return default; // or throw an exception if you prefer
    }


    public static bool IsProtein(Ingredient ingredient)
    {
        return ingredient == Ingredient.WerewolfSteak || 
            ingredient == Ingredient.WerewolfTail || 
            ingredient == Ingredient.YetiRibs || 
            ingredient == Ingredient.YetiDrumstick;
    }

    public static bool IsVegetable(Ingredient ingredient)
    {
        return !IsProtein(ingredient);
    }
}