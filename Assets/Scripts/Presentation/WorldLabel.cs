using UnityEngine;

namespace Shields.Presentation
{
    /// <summary>
    /// Prototype world-space text: always turned toward the viewing camera, optionally
    /// removed after a lifetime. It only displays strings it is handed; it has no idea
    /// what they mean and holds no gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldLabel : MonoBehaviour
    {
        const float ReferenceDistance = 1.8f;

        static Font _font;

        TextMesh _text;
        float _destroyAt = -1f;

        /// <summary>Creates a label under a parent, offset above it in world space.</summary>
        public static WorldLabel Create(string name, Transform parent, Vector3 worldOffset, Color color, float characterSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (parent != null ? parent.position : Vector3.zero) + worldOffset;

            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            TextMesh text = go.AddComponent<TextMesh>();
            text.font = _font;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;

            WorldLabel label = go.AddComponent<WorldLabel>();
            label._text = text;
            return label;
        }

        public string Text
        {
            get { return _text != null ? _text.text : string.Empty; }
            set { if (_text != null) _text.text = value; }
        }

        public Color Color
        {
            set { if (_text != null) _text.color = value; }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>Removes the label after the given number of seconds.</summary>
        public void Expire(float seconds)
        {
            _destroyAt = Time.time + seconds;
        }

        void LateUpdate()
        {
            if (_destroyAt >= 0f && Time.time >= _destroyAt)
            {
                Destroy(gameObject);
                return;
            }

            Camera view = Camera.main;
            if (view == null) return;

            // TextMesh reads correctly when its forward points away from the viewer.
            Vector3 away = transform.position - view.transform.position;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, view.transform.up);

            // Roughly constant on-screen size, so a neighbour's label is not huge.
            transform.localScale = Vector3.one * Mathf.Clamp(away.magnitude / ReferenceDistance, 0.3f, 1.2f);
        }
    }
}
