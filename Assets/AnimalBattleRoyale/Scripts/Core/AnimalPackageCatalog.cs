using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class AnimalPackageCatalog : ScriptableObject
    {
        [SerializeField] private GameObject tiger;
        [SerializeField] private GameObject ant;
        [SerializeField] private GameObject eagle;
        [SerializeField] private GameObject monkey;

        public GameObject GetPrefab(AnimalType type) => type switch
        {
            AnimalType.Tiger => tiger,
            AnimalType.Ant => ant,
            AnimalType.Eagle => eagle,
            AnimalType.Monkey => monkey,
            _ => null
        };
    }
}
