using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NavegacaoRenal
{
    public sealed class ExplorationVisibilityController : MonoBehaviour
    {
        [Serializable]
        public sealed class ExteriorRendererBinding
        {
            public Renderer renderer;
            public Material[] opaqueMaterials;
            public Material[] transparentMaterials;
        }

        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private ExteriorRendererBinding[] exteriorBindings;
        [SerializeField] private GameObject collectingSystem;
        [SerializeField] private GameObject stone;
        [SerializeField] private ExteriorVisibilityMode exteriorMode = ExteriorVisibilityMode.Transparent;
        [SerializeField] private bool collectingSystemVisible = true;
        [SerializeField] private bool stoneVisible = true;
        [SerializeField] private bool panelExpanded = true;

        public event Action Changed;

        public ExteriorVisibilityMode ExteriorMode => exteriorMode;
        public bool CollectingSystemVisible => collectingSystemVisible;
        public bool StoneVisible => stoneVisible;
        public bool PanelExpanded => panelExpanded;
        public ExteriorRendererBinding[] ExteriorBindings => exteriorBindings;
        public GameObject CollectingSystem => collectingSystem;
        public GameObject Stone => stone;

        public void Configure(KidneyGameManager manager, ExteriorRendererBinding[] bindings, GameObject interior, GameObject targetStone)
        {
            gameManager = manager;
            exteriorBindings = bindings;
            collectingSystem = interior;
            stone = targetStone;
        }

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentMode != KidneyGameMode.Exploration || Keyboard.current == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) CycleExteriorMode();
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) ToggleCollectingSystem();
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) gameManager.ToggleRoute();
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) ToggleStone();
            if (keyboard.hKey.wasPressedThisFrame) TogglePanel();
        }

        public void ResetDefaults()
        {
            exteriorMode = ExteriorVisibilityMode.Transparent;
            collectingSystemVisible = true;
            stoneVisible = true;
            panelExpanded = true;
            ApplyAll();
        }

        public void CycleExteriorMode()
        {
            SetExteriorMode(exteriorMode == ExteriorVisibilityMode.Transparent
                ? ExteriorVisibilityMode.Opaque
                : exteriorMode == ExteriorVisibilityMode.Opaque
                    ? ExteriorVisibilityMode.Hidden
                    : ExteriorVisibilityMode.Transparent);
        }

        public void SetExteriorMode(ExteriorVisibilityMode mode)
        {
            exteriorMode = mode;
            ApplyExterior();
            Changed?.Invoke();
        }

        public void ToggleCollectingSystem() => SetCollectingSystemVisible(!collectingSystemVisible);

        public void SetCollectingSystemVisible(bool visible)
        {
            collectingSystemVisible = visible;
            if (collectingSystem != null) collectingSystem.SetActive(visible);
            Changed?.Invoke();
        }

        public void ToggleStone() => SetStoneVisible(!stoneVisible);

        public void SetStoneVisible(bool visible)
        {
            stoneVisible = visible;
            if (stone != null) stone.SetActive(visible);
            Changed?.Invoke();
        }

        public void TogglePanel()
        {
            panelExpanded = !panelExpanded;
            Changed?.Invoke();
        }

        private void ApplyAll()
        {
            ApplyExterior();
            if (collectingSystem != null) collectingSystem.SetActive(collectingSystemVisible);
            if (stone != null) stone.SetActive(stoneVisible);
            Changed?.Invoke();
        }

        private void ApplyExterior()
        {
            if (exteriorBindings == null)
                return;
            foreach (ExteriorRendererBinding binding in exteriorBindings)
            {
                if (binding == null || binding.renderer == null)
                    continue;
                binding.renderer.enabled = exteriorMode != ExteriorVisibilityMode.Hidden;
                if (!binding.renderer.enabled)
                    continue;
                Material[] selected = exteriorMode == ExteriorVisibilityMode.Transparent
                    ? binding.transparentMaterials
                    : binding.opaqueMaterials;
                if (selected != null && selected.Length > 0)
                    binding.renderer.sharedMaterials = selected;
            }
        }
    }
}
