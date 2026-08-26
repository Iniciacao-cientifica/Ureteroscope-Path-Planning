using UnityEngine;
using UnityEngine.SceneManagement;

namespace NavegacaoRenal
{
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        private void OnGUI()
        {
            float width = 430f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.24f;
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUIStyle subtitle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(x, y, width, 52), "Navegacao Renal 3D", title);
            GUI.Label(new Rect(x, y + 54, width, 38), "Prototipo com mouse - nivel facil", subtitle);
            if (GUI.Button(new Rect(x + 55, y + 118, width - 110, 48), "Abrir simulacao"))
                SceneManager.LoadScene("KidneyGame");
            GUI.Label(new Rect(x, y + 180, width, 50), "F1: modo realista  |  F2: exploracao livre", subtitle);
        }
    }
}
