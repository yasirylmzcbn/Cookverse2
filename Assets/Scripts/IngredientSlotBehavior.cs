using UnityEngine;

namespace Cookverse.Assets.Scripts
{
    public abstract class IngredientSlotBehaviour : MonoBehaviour, IIngredientSlot
    {
        public abstract float SnapRange { get; }
        public abstract bool CanAcceptIngredient(KitchenIngredientController ingredient);
        public abstract bool IsWithinSnapRange(Vector3 ingredientWorldPos);
        public abstract float DistanceToAnchor(Vector3 worldPos, KitchenIngredientController ingredient);
        public abstract bool TryPlaceIngredient(KitchenIngredientController ingredient);
        public abstract bool RemoveIngredient(KitchenIngredientController ingredient);
    }
}