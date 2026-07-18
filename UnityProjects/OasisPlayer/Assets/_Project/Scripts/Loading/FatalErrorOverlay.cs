using UnityEngine;

namespace OasisPlayer.Loading;

public sealed class FatalErrorOverlay : MonoBehaviour
{
    private string _message;
    public void Show(string message) { _message = message; Debug.LogError(message); enabled = true; }
    private void OnGUI() { if (string.IsNullOrEmpty(_message)) return; GUI.color = Color.white; GUI.Box(new Rect(20,20,Screen.width-40,120), "Oasis Player startup error\n\n" + _message); }
}
