using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    // One focus scope per visible menu; the existing InputSystemUIInputModule handles movement/submit.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FightMenuNavigation : MonoBehaviour
    {
        public bool Ready { get; set; } = true;
        public bool Armed { get; private set; }
        CanvasGroup group;
        Button[] buttons;
        int releasedFrame = -1;
        public static bool ConfirmHeld => (Gamepad.current?.buttonSouth.isPressed ?? false) ||
            (Keyboard.current?.enterKey.isPressed ?? false) || (Keyboard.current?.spaceKey.isPressed ?? false);
        void EnsureGroup()
        {
            // Unity's missing/destroyed objects must use its overloaded null check, not ??.
            if (!group) group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
        }
        void OnEnable()
        {
            EnsureGroup();
            Armed = false;
            releasedFrame = -1;
            group.interactable = false;
            group.blocksRaycasts = true;
        }
        void Update()
        {
            EnsureGroup();
            AdvanceConfirmation(ConfirmHeld, Time.frameCount);
            group.interactable = Armed;
            group.blocksRaycasts = true; // Never click through a locked menu.
            var events = EventSystem.current;
            if (!events) return;
            if (!Armed)
            {
                if (events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(transform))
                    events.SetSelectedGameObject(null);
                return;
            }
            buttons ??= GetComponentsInChildren<Button>(true);
            Button first = null;
            foreach (var button in buttons)
            {
                if (!Eligible(button)) continue;
                if (!first) first = button;
                button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = Nearest(button, Vector2.up), selectOnDown = Nearest(button, Vector2.down),
                    selectOnLeft = Nearest(button, Vector2.left), selectOnRight = Nearest(button, Vector2.right) };
            }
            var selected = events.currentSelectedGameObject;
            if (first && (!selected || !selected.transform.IsChildOf(transform) || !Eligible(selected.GetComponent<Button>())))
                events.SetSelectedGameObject(first.gameObject);
        }
        private void AdvanceConfirmation(bool held, int frame)
        {
            if (!Ready) { Armed = false; releasedFrame = -1; }
            else if (!Armed)
            {
                if (held) releasedFrame = -1;
                else if (releasedFrame < 0) releasedFrame = frame;
                else if (frame > releasedFrame) Armed = true;
            }
        }
        bool Eligible(Button button) => button && button.gameObject.activeInHierarchy && button.interactable;
        Button Nearest(Button source, Vector2 direction)
        {
            Button nearest = null; float best = float.PositiveInfinity;
            foreach (var candidate in buttons)
            {
                if (candidate == source || !Eligible(candidate)) continue;
                Vector2 delta = transform.InverseTransformPoint(candidate.transform.position) - transform.InverseTransformPoint(source.transform.position);
                float forward = Vector2.Dot(delta, direction);
                if (forward <= .01f) continue;
                float perpendicular = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                float score = delta.sqrMagnitude / forward + perpendicular * 2;
                if (score < best) { best = score; nearest = candidate; }
            }
            return nearest;
        }
        void OnDisable()
        {
            Armed = false; releasedFrame = -1;
            var events = EventSystem.current;
            if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(transform))
                events.SetSelectedGameObject(null);
        }
    }
}
