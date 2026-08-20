using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterPrototypeHud : MonoBehaviour
    {
        [SerializeField] private OtterMovementController movement;
        [SerializeField] private bool visible = true;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;

        public void Configure(OtterMovementController movementController)
        {
            movement = movementController;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(new Rect(20f, Screen.height - 42f, 620f, 28f), "WASD / Arrow Keys / Left Stick  •  SPACE / South Button: Belly Slide  •  F1: HUD", labelStyle);
            if (!visible || movement == null)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(0.035f, 0.12f, 0.14f, 0.88f);
            GUI.Box(new Rect(20f, 20f, 300f, 150f), GUIContent.none, panelStyle);
            GUI.color = previousColor;
            GUI.Label(new Rect(38f, 32f, 260f, 30f), "SEA OTTER MOVEMENT LAB", titleStyle);
            GUI.Label(new Rect(38f, 67f, 260f, 24f), $"SURFACE   {movement.CurrentSurface.ToString().ToUpperInvariant()}", labelStyle);
            GUI.Label(new Rect(38f, 91f, 260f, 24f), $"SPEED      {movement.Speed:0.0}", labelStyle);
            GUI.Label(new Rect(38f, 115f, 260f, 24f), $"WETNESS  {movement.Wetness01 * 100f:0}%", labelStyle);
            GUI.Label(new Rect(38f, 139f, 260f, 24f), movement.IsSliding ? "STATE       BELLY SLIDE" : "STATE       MOVING", labelStyle);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
                return;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = Texture2D.whiteTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.padding = new RectOffset(14, 14, 12, 12);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.25f, 0.94f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.97f, 1f) }
            };
        }
    }
}
