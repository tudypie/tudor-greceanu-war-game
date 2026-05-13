using UnityEngine;
using UnityEngine.InputSystem;

public class HudToggle : MonoBehaviour
{
    public static bool Visible = true;

    public Key ToggleKey = Key.F9;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[ToggleKey].wasPressedThisFrame) Visible = !Visible;
    }
}
