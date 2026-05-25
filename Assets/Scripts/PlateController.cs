using Cookverse.Assets.Scripts;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class PlateController : IngredientSlotBehaviour, IDualAnchorIngredientSlot
{
    [Header("Audio")]
    [SerializeField] private AudioClip placeOnPlateSfx;
    [SerializeField, Range(0f, 1f)] private float placeOnPlateSfxVolume = 1f;
    private AudioSource _audioSource;

    [Header("Recipe")]
    [SerializeField] public Recipe recipe; // Optional: leave empty for universal plate that works with all recipes
    private List<Ingredient> requiredIngredients;
    private Recipe _detectedRecipe; // The recipe currently being made (auto-detected if recipe field is not set)

    [Header("Placement")]
    [Tooltip("Where the protein ingredient snaps to (create an empty child and assign it).")]
    public Transform proteinAnchor;
    [Tooltip("Where the vegetable ingredient snaps to (create an empty child and assign it).")]
    public Transform vegetableAnchor;
    [Tooltip("How close an ingredient must be (to the anchor) to snap.")]
    public float snapRange = 0.8f;

    private KitchenIngredientController proteinIngredient;
    private KitchenIngredientController vegetableIngredient;

    [Header("Recipe Completion TMP")]
    [SerializeField] public TextMeshProUGUI completionText;

    [Header("Unlock + Spell Grant")]
    [Tooltip("Optional override; if left empty, this Plate will find the player's PlayerRecipeUnlocks in the scene.")]
    [SerializeField] private PlayerRecipeUnlocks playerRecipeUnlocks;
    [Tooltip("Optional. If set and a RecipeSpellDatabase is provided, the unlocked recipe's spell can be equipped.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Optional database to map recipes -> spells.")]
    [SerializeField] private RecipeSpellDatabase recipeSpellDatabase;

    [Tooltip("If true, when a recipe is completed its mapped spell will be equipped (if available).")]
    [SerializeField] private bool autoEquipUnlockedSpell = true;

    private Recipe _unlockedRecipe = default; // Tracks which recipe was unlocked to allow multiple unlocks

    public Transform ProteinAnchor => proteinAnchor;
    public Transform VegetableAnchor => vegetableAnchor;
    public override float SnapRange => snapRange;

    public Transform GetProteinAnchor() => proteinAnchor != null ? proteinAnchor : transform;
    public Transform GetVegetableAnchor() => vegetableAnchor != null ? vegetableAnchor : transform;

    private void Awake()
    {
        RebindReferences();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureAudioSource();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindReferences();
    }

    private void RebindReferences()
    {
        if (PlayerRecipeUnlocks.Instance != null)
            playerRecipeUnlocks = PlayerRecipeUnlocks.Instance;
        else if (playerRecipeUnlocks == null)
            playerRecipeUnlocks = FindFirstObjectByType<PlayerRecipeUnlocks>();

        if (PlayerController.Instance != null)
            playerController = PlayerController.Instance;
        else if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
    }

    public void Start()
    {
        // If recipe is explicitly set, pre-load its ingredients
        if (!recipe.Equals(default(Recipe)))
        {
            requiredIngredients = Recipes.GetIngredientsForRecipe(recipe);
        }
        ValidateRecipeCompletion();
    }

    /// <summary>
    /// Finds which recipe matches the currently placed ingredients.
    /// Returns the matching recipe, or the explicitly set recipe if one exists.
    /// </summary>
    private Recipe FindMatchingRecipe()
    {
        // If recipe is explicitly set, use that
        if (!recipe.Equals(default(Recipe)))
            return recipe;

        // If no ingredients placed yet, return default
        if (proteinIngredient == null || vegetableIngredient == null)
            return default;

        // Try to find a recipe that matches the placed ingredients
        foreach (var recipeEntry in Recipes.RecipeIngredients)
        {
            Recipe potentialRecipe = recipeEntry.Key;
            List<Ingredient> ingredientsNeeded = recipeEntry.Value;

            if (ingredientsNeeded.Contains(proteinIngredient.IngredientType) &&
                ingredientsNeeded.Contains(vegetableIngredient.IngredientType))
            {
                return potentialRecipe;
            }
        }

        return default;
    }
    public override bool CanAcceptIngredient(KitchenIngredientController ingredient)
    {
        if (ingredient == null) return false;
        if (!ingredient.IsCooked()) return false;

        bool isProtein = ingredient.IsProteinIngredient;
        bool isVegetable = ingredient.IsVegetableIngredient;

        if (isProtein && !HasProteinIngredient()) return true;
        if (isVegetable && !HasVegetableIngredient()) return true;

        return false;
    }

    private bool HasProteinIngredient()
    {
        return proteinIngredient != null;
    }

    private bool HasVegetableIngredient()
    {
        return vegetableIngredient != null;
    }

    public override float DistanceToAnchor(Vector3 worldPos, KitchenIngredientController ingredient)
    {
        if (ingredient == null) return float.PositiveInfinity;
        Transform anchor = ingredient.IsProteinIngredient ? GetProteinAnchor() : GetVegetableAnchor();
        return Vector3.Distance(worldPos, anchor.position);
    }

    public override bool IsWithinSnapRange(Vector3 ingredientWorldPos)
    {
        float proteinDistance = Vector3.Distance(ingredientWorldPos, GetProteinAnchor().position);
        float vegetableDistance = Vector3.Distance(ingredientWorldPos, GetVegetableAnchor().position);
        return proteinDistance <= snapRange || vegetableDistance <= snapRange;
    }

    public override bool TryPlaceIngredient(KitchenIngredientController ingredient)
    {
        Debug.Log("Trying to place ingredient: " + ingredient?.name);
        if (ingredient == null) return false;
        if (!ingredient.IsCooked()) return false;

        bool isProtein = ingredient.IsProteinIngredient;
        bool isVegetable = ingredient.IsVegetableIngredient;

        if (isProtein && !HasProteinIngredient())
        {
            proteinIngredient = ingredient;
            ingredient.SnapInto(GetProteinAnchor());
            PlayOneShot(placeOnPlateSfx, placeOnPlateSfxVolume);
            ValidateRecipeCompletion();
            return true;
        }
        else if (isVegetable && !HasVegetableIngredient())
        {
            vegetableIngredient = ingredient;
            ingredient.SnapInto(GetVegetableAnchor());
            PlayOneShot(placeOnPlateSfx, placeOnPlateSfxVolume);
            ValidateRecipeCompletion();
            return true;
        }

        return false;
    }

    public override bool RemoveIngredient(KitchenIngredientController ingredient)
    {
        if (ingredient == null) return false;


        if (proteinIngredient == ingredient)
        {
            proteinIngredient = null;
            ingredient.OnRemovedFromSlot();
            ValidateRecipeCompletion();
            return true;
        }

        if (vegetableIngredient == ingredient)
        {
            vegetableIngredient = null;
            ingredient.OnRemovedFromSlot();
            ValidateRecipeCompletion();
            return true;
        }

        return false;
    }

    public bool IsRecipeComplete()
    {
        RebindReferences();

        if (proteinIngredient == null || vegetableIngredient == null) return false;

        // Determine which recipe is being made
        _detectedRecipe = FindMatchingRecipe();
        if (_detectedRecipe.Equals(default(Recipe)))
        {
            Debug.Log($"No matching recipe found for ingredients: protein={proteinIngredient.IngredientType}, vegetable={vegetableIngredient.IngredientType}");
            return false;
        }

        requiredIngredients = Recipes.GetIngredientsForRecipe(_detectedRecipe);

        bool hasRequiredProtein = requiredIngredients.Contains(proteinIngredient.IngredientType);
        bool hasRequiredVegetable = requiredIngredients.Contains(vegetableIngredient.IngredientType);
        bool complete = hasRequiredProtein && hasRequiredVegetable;
        Debug.Log($"Recipe check for {_detectedRecipe}: protein={proteinIngredient.IngredientType} (required={hasRequiredProtein}), vegetable={vegetableIngredient.IngredientType} (required={hasRequiredVegetable}), complete={complete}");
        
        // Check if this is a new recipe (different from what was previously unlocked)
        bool isNewRecipe = !_unlockedRecipe.Equals(_detectedRecipe);
        
        if (complete && isNewRecipe)
        {
            _unlockedRecipe = _detectedRecipe; // Mark this recipe as unlocked
            Debug.Log($"*** RECIPE COMPLETE: {_detectedRecipe} - Triggering unlock sequence ***");

            bool unlockedNow = false;

            if (playerRecipeUnlocks != null)
            {
                unlockedNow = playerRecipeUnlocks.Unlock(_detectedRecipe);
                Debug.Log($"Unlock result for {_detectedRecipe}: {unlockedNow}");
            }
            else
                Debug.LogWarning($"No PlayerRecipeUnlocks found. Recipe '{_detectedRecipe}' won't be saved as unlocked.");

            // Try to auto-equip the spell if enabled
            if (autoEquipUnlockedSpell && recipeSpellDatabase != null && playerController != null)
            {
                SpellDefinition spell = recipeSpellDatabase.GetSpellOrNull(_detectedRecipe);
                if (spell != null)
                {
                    Debug.Log($"Found spell {spell.name} for recipe {_detectedRecipe}. Attempting auto-equip...");
                    // Find first empty spell slot and equip there
                    bool spellEquipped = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (playerController.GetSpell(i) == null)
                        {
                            spellEquipped = playerController.TryEquipSpell(spell, i);
                            Debug.Log($"Auto-equip spell {spell.name} to slot {i}: {spellEquipped}");
                            break;
                        }
                    }
                    if (!spellEquipped)
                        Debug.LogWarning($"Could not auto-equip spell {spell.name} - all slots full or TryEquipSpell failed");
                }
                else
                    Debug.LogWarning($"No spell found for recipe {_detectedRecipe}");
            }
            else
                Debug.Log($"Auto-equip disabled or missing components. autoEquipUnlockedSpell={autoEquipUnlockedSpell}, db={recipeSpellDatabase != null}, controller={playerController != null}");

            if (completionText != null)
            {
                completionText.text = "You unlocked the " + _detectedRecipe.ToString() + " recipe!";
                Debug.Log($"Displaying completion text for {_detectedRecipe}");
                StartCoroutine(HideTextCoroutine(5f));
            }
            else
                Debug.LogWarning("No completion text UI assigned to plate");
        }
        else if (!complete)
        {
            _unlockedRecipe = default; // Reset when recipe is no longer complete
        }

        return complete;
    }

    private void ValidateRecipeCompletion()
    {
        IsRecipeComplete();
    }

    private System.Collections.IEnumerator HideTextCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (completionText != null)
            completionText.text = "";
    }

    private void EnsureAudioSource()
    {
        if (_audioSource != null)
            return;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        EnsureAudioSource();
        if (_audioSource != null)
            _audioSource.PlayOneShot(clip, volume);
    }
}

