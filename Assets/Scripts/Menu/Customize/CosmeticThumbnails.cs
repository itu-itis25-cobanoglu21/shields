using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shields.Menu
{
    /// <summary>
    /// Renders small transparent item icons from real models at startup, far below the
    /// scene on a private layer. Textures live in memory only.
    /// </summary>
    public sealed class CosmeticThumbnails
    {
        const int StudioLayer = 31;
        static readonly Vector3 StudioPosition = new Vector3(0f, -400f, 0f);

        readonly GameObject _root;
        readonly Camera _camera;

        public CosmeticThumbnails()
        {
            _root = new GameObject("__ThumbnailStudio");
            _root.transform.position = StudioPosition;

            var camGo = new GameObject("StudioCamera");
            camGo.transform.SetParent(_root.transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.cullingMask = 1 << StudioLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0, 0, 0, 0);
            _camera.allowHDR = false;
            _camera.fieldOfView = 20f;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 20f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            AddLight("Key", new Vector3(-1.2f, 1.6f, -1.6f), new Color(1f, 0.82f, 0.62f), 3.2f);
            AddLight("Fill", new Vector3(1.6f, 0.4f, -1.2f), new Color(0.7f, 0.78f, 1f), 1.3f);
            AddLight("Rim", new Vector3(0.6f, 1.4f, 1.8f), new Color(1f, 0.9f, 0.75f), 2.2f);
        }

        void AddLight(string name, Vector3 offset, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = offset;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << StudioLayer;
        }

        /// <summary>Renders <paramref name="subject"/> and destroys it afterwards.</summary>
        public Texture2D Render(GameObject subject, int size, float yaw, float pitch, float zoom = 1f)
        {
            subject.transform.SetParent(_root.transform, false);
            subject.transform.localPosition = Vector3.zero;
            SetLayer(subject.transform);

            Bounds b = CharacterCosmetics.WorldBounds(subject);
            float radius = Mathf.Max(0.001f, b.extents.magnitude);
            float distance = radius / Mathf.Sin(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.92f / zoom;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            _camera.transform.position = b.center + rot * new Vector3(0f, 0f, -distance);
            _camera.transform.rotation = rot;

            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            _camera.targetTexture = rt;
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Thumb_" + subject.name };
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            // The clear colour is transparent black, so edge pixels come out premultiplied.
            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte a = px[i].a;
                if (a == 0 || a == 255) continue;
                float k = 255f / a;
                px[i].r = (byte)Mathf.Min(255, px[i].r * k);
                px[i].g = (byte)Mathf.Min(255, px[i].g * k);
                px[i].b = (byte)Mathf.Min(255, px[i].b * k);
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);

            subject.SetActive(false);
            Object.Destroy(subject);
            return tex;
        }

        public void Dispose() { Object.Destroy(_root); }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = StudioLayer;
            foreach (Transform c in t) SetLayer(c);
        }
    }
}
