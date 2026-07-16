using UnityEngine;

namespace AnimalBattleRoyale
{
    public static class AnimalFactory
    {
        public static ThirdPersonAnimalController Create(
            string objectName,
            AnimalType type,
            Vector3 position,
            bool isLocalPlayer,
            Transform cameraTransform = null,
            bool addBotAI = true)
        {
            GameObject root = new GameObject(objectName);
            root.transform.position = position;

            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.minMoveDistance = 0f;

            Health health = root.AddComponent<Health>();
            ThirdPersonAnimalController controller = root.AddComponent<ThirdPersonAnimalController>();
            controller.Initialize(type, isLocalPlayer, cameraTransform);

            // CharacterController uses the root as its feet position. Snap after its height
            // has been configured so every animal begins on top of the generated terrain.
            controller.SnapToTerrain(Object.FindAnyObjectByType<JungleGenerator>());

            if (!isLocalPlayer && addBotAI)
            {
                root.AddComponent<SimpleBotAI>();
            }

            BattleRoyaleManager.Instance?.RegisterFighter(controller);
            return controller;
        }
    }
}
