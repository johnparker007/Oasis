// TabNextSelection.cs
// Adapted from a forum posting by SirRogers:
//   https://forum.unity.com/threads/
//       tab-between-input-fields.263779/#post-2404236
//

namespace Oasis.UI
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    public class TabNextSelection : MonoBehaviour
    {
        [Tooltip("Also support Shift+Tab to move backwards to prior selection")]
        public bool BackTab = true;

        private EventSystem system;

        private void OnEnable()
        {
            system = EventSystem.current;
        }

        private bool WasTabPressed()
        {
#if ENABLE_INPUT_SYSTEM
        bool tab =
            Keyboard.current.tabKey.wasPressedThisFrame;
#else
            bool tab =
                Input.GetKeyDown(KeyCode.Tab);
#endif
            return tab;
        }

        private bool IsShiftPressed()
        {
#if ENABLE_INPUT_SYSTEM
        bool shift =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;
#else
            bool shift =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
#endif
            return shift;
        }

        private Selectable PriorSelectable(Selectable current)
        {
            Selectable prior = current.FindSelectableOnLeft();
            if (prior == null)
                prior = current.FindSelectableOnUp();
            return prior;
        }

        private Selectable NextSelectable(Selectable current)
        {
            Selectable next = current.FindSelectableOnRight();
            if (next == null)
                next = current.FindSelectableOnDown();
            return next;
        }

        private void Update()
        {
            if (system == null)
                return;

            GameObject selected = system.currentSelectedGameObject;
            if (selected == null)
                return;

            if (!WasTabPressed())
                return;

            Selectable current = selected.GetComponent<Selectable>();
            if (current == null)
                return;

            bool up = IsShiftPressed();
            Selectable next = up ? PriorSelectable(current) : NextSelectable(current);
            // Wrap from end to beginning, or vice versa.
            if (next == null)
            {
                next = current;
                Selectable pnext;
                if (up)
                    while ((pnext = NextSelectable(next)) != null)
                        next = pnext;
                else
                    while ((pnext = PriorSelectable(next)) != null)
                        next = pnext;
            }

            if (next == null)
                return;

            // Simulate mouse click for InputFields.
            InputField inputfield = next.GetComponent<InputField>();
            if (inputfield != null)
                inputfield.OnPointerClick(new PointerEventData(system));

            // Select the next item in the tab-order of our direction.
            system.SetSelectedGameObject(next.gameObject);
        }
    }
}
