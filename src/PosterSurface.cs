using UnityEngine;

namespace KitsunePosters
{
    /// <summary>
    /// Puts a downloaded image on a sign face: hides the sign's TextMesh and
    /// parents a textured quad to it. The quad dies with the sign's GameObject
    /// on chunk unload; RefreshTextMesh re-creates it on reload.
    /// </summary>
    public static class PosterSurface
    {
        private const string QuadName = "KitsunePosterQuad";

        // The quad sits this far in front of the text anchor, along the same
        // axis the text faces. If posters render inside the sign board in
        // game, flip the sign of this constant.
        private const float FaceOffset = -0.005f;

        private static Shader posterShader;

        public static void Apply(SmartTextMesh[] _meshes, Texture2D _tex, float _width)
        {
            // Fit the image into a square box of the sign's line width.
            float w = _width;
            float h = _width * ((float)_tex.height / _tex.width);
            if (h > _width)
            {
                w = _width * ((float)_tex.width / _tex.height);
                h = _width;
            }

            foreach (var mesh in _meshes)
            {
                if (mesh == null)
                {
                    continue; // destroyed with its chunk while we downloaded
                }
                SetTextVisible(mesh, false);
                var quad = FindOrCreateQuad(mesh.transform);
                quad.transform.localScale = new Vector3(w, h, 1f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.material.mainTexture = _tex;
                renderer.enabled = true;
            }
        }

        public static void HideText(SmartTextMesh[] _meshes)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh != null)
                {
                    SetTextVisible(mesh, false);
                }
            }
        }

        public static void ShowText(SmartTextMesh[] _meshes)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh != null)
                {
                    SetTextVisible(mesh, true);
                }
            }
        }

        /// <summary>No URL on the sign: back to a plain text sign.</summary>
        public static void Clear(SmartTextMesh[] _meshes)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh == null)
                {
                    continue;
                }
                SetTextVisible(mesh, true);
                var old = mesh.transform.Find(QuadName);
                if (old != null)
                {
                    Object.Destroy(old.gameObject);
                }
            }
        }

        private static void SetTextVisible(SmartTextMesh _mesh, bool _visible)
        {
            var renderer = _mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = _visible;
            }
        }

        private static GameObject FindOrCreateQuad(Transform _anchor)
        {
            var existing = _anchor.Find(QuadName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = QuadName;
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider); // never block targeting the sign itself
            }
            quad.layer = _anchor.gameObject.layer;
            quad.transform.SetParent(_anchor, false);
            quad.transform.localPosition = new Vector3(0f, 0f, FaceOffset);
            quad.transform.localRotation = Quaternion.identity;

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.material = new Material(PosterShader());
            return quad;
        }

        private static Shader PosterShader()
        {
            if (posterShader != null)
            {
                return posterShader;
            }
            // Sprites/Default is unlit, tintable and double-sided, so quad
            // orientation can't hide the image entirely; the others are
            // fallbacks in case it was stripped from the build.
            foreach (var name in new[] { "Sprites/Default", "Unlit/Texture", "UI/Default" })
            {
                posterShader = Shader.Find(name);
                if (posterShader != null)
                {
                    Log.Out($"[KitsunePosters] Using shader '{name}' for posters.");
                    return posterShader;
                }
            }
            Log.Warning("[KitsunePosters] No unlit shader found; posters will render with the primitive default material.");
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            posterShader = probe.GetComponent<MeshRenderer>().material.shader;
            Object.Destroy(probe);
            return posterShader;
        }
    }
}
