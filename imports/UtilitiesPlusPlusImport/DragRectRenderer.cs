using UnityEngine;

namespace UtilitiesPP
{
    public class DragRectRenderer : MonoBehaviour
    {
        public bool IsActive;
        public Vector2 StartPos;
        public Vector2 EndPos;

        private Texture2D _fillTexture;
        private Texture2D _borderTexture;

        void Awake()
        {
            _fillTexture = new Texture2D(1, 1);
            _fillTexture.SetPixel(0, 0, new Color(1f, 0.85f, 0.2f, 0.15f));
            _fillTexture.Apply();

            _borderTexture = new Texture2D(1, 1);
            _borderTexture.SetPixel(0, 0, new Color(1f, 0.85f, 0.2f, 0.8f));
            _borderTexture.Apply();
        }

        void OnGUI()
        {
            if (!IsActive) return;

            float x = Mathf.Min(StartPos.x, EndPos.x);
            float y = Mathf.Min(StartPos.y, EndPos.y);
            float w = Mathf.Abs(EndPos.x - StartPos.x);
            float h = Mathf.Abs(EndPos.y - StartPos.y);

            if (w < 2f && h < 2f) return;

            var rect = new Rect(x, y, w, h);
            GUI.DrawTexture(rect, _fillTexture);

            float b = 2f;
            GUI.DrawTexture(new Rect(x, y, w, b), _borderTexture);
            GUI.DrawTexture(new Rect(x, y + h - b, w, b), _borderTexture);
            GUI.DrawTexture(new Rect(x, y, b, h), _borderTexture);
            GUI.DrawTexture(new Rect(x + w - b, y, b, h), _borderTexture);
        }

        public void SetRect(Vector2 start, Vector2 end)
        {
            StartPos = start;
            EndPos = end;
        }

        public void Show()
        {
            IsActive = true;
        }

        public void Hide()
        {
            IsActive = false;
        }

        void OnDestroy()
        {
            if (_fillTexture != null) Destroy(_fillTexture);
            if (_borderTexture != null) Destroy(_borderTexture);
        }
    }
}
