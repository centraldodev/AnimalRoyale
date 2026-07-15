using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class AnimalPackageCatalog : ScriptableObject
    {
        [SerializeField] private GameObject tiger;
        [SerializeField] private GameObject deer;
        [SerializeField] private GameObject horse;
        [SerializeField] private GameObject chicken;
        [SerializeField] private GameObject dog;
        [SerializeField] private GameObject cat;
        [SerializeField] private GameObject penguin;

        public GameObject GetPrefab(AnimalType type) => type switch
        {
            AnimalType.Tiger => tiger,
            AnimalType.Deer => deer,
            AnimalType.Horse => horse,
            AnimalType.Chicken => chicken,
            AnimalType.Dog => dog,
            AnimalType.Cat => cat,
            AnimalType.Penguin => penguin,
            _ => null
        };
    }
}
