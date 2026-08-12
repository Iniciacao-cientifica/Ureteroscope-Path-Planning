using UnityEngine;

public enum VrMenuAction
{
    PreviousCase,
    NextCase,
    ToggleAnatomy,
    ToggleStones,
    ToggleRoute,
    NextRoute,
    ToggleAnimation,
    ResetView,
    OpacityDown,
    OpacityUp
}

public class VrMenuButton : MonoBehaviour
{
    public VrCaseLoader loader;
    public VrMenuAction action;

    public void Activate()
    {
        if (loader == null)
        {
            return;
        }
        switch (action)
        {
            case VrMenuAction.PreviousCase: loader.PreviousCase(); break;
            case VrMenuAction.NextCase: loader.NextCase(); break;
            case VrMenuAction.ToggleAnatomy: loader.ToggleAnatomy(); break;
            case VrMenuAction.ToggleStones: loader.ToggleStones(); break;
            case VrMenuAction.ToggleRoute: loader.ToggleRouteVisibility(); break;
            case VrMenuAction.NextRoute: loader.NextRoute(); break;
            case VrMenuAction.ToggleAnimation: loader.ToggleRouteFollow(); break;
            case VrMenuAction.ResetView: loader.ResetView(); break;
            case VrMenuAction.OpacityDown: loader.AdjustMeshOpacity(-0.08f); break;
            case VrMenuAction.OpacityUp: loader.AdjustMeshOpacity(0.08f); break;
        }
    }
}

public class VrWorldMenu : MonoBehaviour
{
    public VrCaseLoader loader;
    public float distance = 0.75f;
    public float buttonWidth = 0.15f;
    public float buttonHeight = 0.055f;

    private void Start()
    {
        if (loader == null)
        {
            loader = FindAnyObjectByType<VrCaseLoader>();
        }
        BuildMenu();
        PositionMenu();
        gameObject.AddComponent<VrBillboard>();
    }

    private void BuildMenu()
    {
        string[] labels =
        {
            "< Case", "Case >", "Anatomy", "Stones", "Route",
            "Next route", "Play / Stop", "Reset", "Opacity -", "Opacity +"
        };
        VrMenuAction[] actions =
        {
            VrMenuAction.PreviousCase, VrMenuAction.NextCase, VrMenuAction.ToggleAnatomy,
            VrMenuAction.ToggleStones, VrMenuAction.ToggleRoute, VrMenuAction.NextRoute,
            VrMenuAction.ToggleAnimation, VrMenuAction.ResetView, VrMenuAction.OpacityDown,
            VrMenuAction.OpacityUp
        };

        for (int index = 0; index < labels.Length; index++)
        {
            int column = index % 2;
            int row = index / 2;
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = labels[index];
            button.transform.SetParent(transform, false);
            button.transform.localPosition = new Vector3((column - 0.5f) * (buttonWidth + 0.012f), -row * (buttonHeight + 0.012f), 0f);
            button.transform.localScale = new Vector3(buttonWidth, buttonHeight, 0.012f);
            Renderer renderer = button.GetComponent<Renderer>();
            renderer.material.color = new Color(0.08f, 0.19f, 0.32f, 1f);

            VrMenuButton menuButton = button.AddComponent<VrMenuButton>();
            menuButton.loader = loader;
            menuButton.action = actions[index];

            GameObject label = new GameObject("Label");
            label.transform.SetParent(button.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.transform.localScale = new Vector3(
                1f / button.transform.localScale.x,
                1f / button.transform.localScale.y,
                1f
            );
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = labels[index];
            text.fontSize = 42;
            text.characterSize = 0.012f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }
    }

    private void PositionMenu()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            transform.position = new Vector3(0.45f, 1.25f, 0.75f);
            return;
        }
        transform.position = camera.transform.position + camera.transform.forward * distance + camera.transform.right * 0.34f + camera.transform.up * 0.18f;
    }
}
