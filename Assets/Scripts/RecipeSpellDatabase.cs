using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Recipes/Recipe Spell Database", fileName = "RecipeSpellDatabase")]
public class RecipeSpellDatabase : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public Recipe recipe;
        public SpellDefinition spell;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<Recipe, SpellDefinition> _map;

    public bool TryGetSpell(Recipe recipe, out SpellDefinition spell)
    {
        EnsureMap();
        return _map.TryGetValue(recipe, out spell) && spell != null;
    }

    public SpellDefinition GetSpellOrNull(Recipe recipe)
    {
        EnsureMap();
        _map.TryGetValue(recipe, out SpellDefinition spell);
        return spell;
    }

    public IReadOnlyList<Entry> GetEntries()
    {
        return entries;
    }

    private void EnsureMap()
    {
        if (_map != null) return;

        _map = new Dictionary<Recipe, SpellDefinition>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            _map[e.recipe] = e.spell;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _map = null;
    }
#endif
}
