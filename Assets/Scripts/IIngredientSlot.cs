using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Cookverse.Assets.Scripts
{
    public interface IIngredientSlot
    {
        public float SnapRange { get; }
        public bool CanAcceptIngredient(KitchenIngredientController ingredient);
        public bool IsWithinSnapRange(Vector3 ingredientWorldPos);
        public bool TryPlaceIngredient(KitchenIngredientController ingredient);
    }

    public interface ISingleAnchorIngredientSlot : IIngredientSlot
    {
        public Transform IngredientAnchor { get; }
        public Transform GetAnchor() => IngredientAnchor != null ? IngredientAnchor : throw new InvalidOperationException("IngredientAnchor is null");
        public float DistanceToAnchor(Vector3 worldPos);
    }

    public interface IDualAnchorIngredientSlot : IIngredientSlot
    {
        public Transform ProteinAnchor { get; }
        public Transform VegetableAnchor { get; }
        public Transform GetProteinAnchor() => ProteinAnchor != null ? ProteinAnchor : throw new InvalidOperationException("ProteinAnchor is null");
        public Transform GetVegetableAnchor() => VegetableAnchor != null ? VegetableAnchor : throw new InvalidOperationException("VegetableAnchor is null");
        public float DistanceToAnchor(Vector3 worldPos, KitchenIngredientController ingredient);

    }
}