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

        // A tiled cell fills the whole 1m block face so adjacent cells meet
        // seamlessly and the image reads as one mural across the group.
        private const float TiledCellSize = 1f;

        private static Vector2[] baseUv;

        // The prefab's text material (KitsunePosters/TextOccluded) ships with
        // no texture; it needs the LIVE font atlas, not a baked copy, or the
        // glyph UVs drift when the dynamic atlas rebuilds. Shared material:
        // one bind covers every poster in the world.
        private static Material textMaterial;
        private static Font textFont;

        public static void BindTextMaterial(SmartTextMesh[] _meshes)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh == null)
                {
                    continue;
                }
                var tm = mesh.GetComponent<TextMesh>();
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (tm == null || renderer == null || tm.font == null)
                {
                    continue;
                }
                var mat = renderer.sharedMaterial;
                if (mat == null || mat.shader == null || !mat.shader.name.EndsWith("TextOccluded"))
                {
                    continue; // vanilla sign or an older bundle: leave it alone
                }
                textMaterial = mat;
                textFont = tm.font;
                var atlas = tm.font.material != null ? tm.font.material.mainTexture : null;
                if (atlas != null && mat.mainTexture != atlas)
                {
                    mat.mainTexture = atlas;
                }
            }
        }

        /// <summary>Dynamic font atlases rebuild at runtime; follow them.</summary>
        public static void OnFontRebuilt(Font _font)
        {
            if (textMaterial != null && _font == textFont && _font.material != null)
            {
                textMaterial.mainTexture = _font.material.mainTexture;
            }
        }

        public static void Apply(SmartTextMesh[] _meshes, Texture2D _tex, float _faceW, float _faceH, PosterLayout _layout, PosterFit _mode, bool _hideBoard = false)
        {
            float w, h;
            if (_layout.Tiled)
            {
                w = TiledCellSize;
                h = TiledCellSize;
            }
            else if (_mode == PosterFit.Fit)
            {
                // Whole image visible, board showing round it.
                w = _faceW;
                h = _faceW * ((float)_tex.height / _tex.width);
                if (h > _faceH)
                {
                    w = _faceH * ((float)_tex.width / _tex.height);
                    h = _faceH;
                }
            }
            else
            {
                w = _faceW;
                h = _faceH;
            }

            // Fill crops the image to the face's aspect (centre crop); a tiled
            // group crops to the whole group's aspect unless stretching.
            var crop = new Rect(0f, 0f, 1f, 1f);
            bool cover = _mode == PosterFit.Fill || (_layout.Tiled && _mode != PosterFit.Stretch);
            if (cover)
            {
                float faceAspect = _layout.Tiled ? (float)_layout.Cols / _layout.Rows : _faceW / _faceH;
                float imageAspect = (float)_tex.width / _tex.height;
                if (imageAspect > faceAspect)
                {
                    float f = faceAspect / imageAspect;
                    crop = new Rect((1f - f) / 2f, 0f, f, 1f);
                }
                else if (imageAspect < faceAspect)
                {
                    float f = imageAspect / faceAspect;
                    crop = new Rect(0f, (1f - f) / 2f, 1f, f);
                }
            }

            float uSize = crop.width / _layout.Cols;
            float vSize = crop.height / _layout.Rows;
            float u0 = crop.x + _layout.Col * uSize;
            float v0 = crop.y + _layout.Row * vSize;

            foreach (var mesh in _meshes)
            {
                if (mesh == null)
                {
                    continue; // destroyed with its chunk while we downloaded
                }
                SetTextVisible(mesh, false);
                if (_hideBoard)
                {
                    // Graffiti: the backing board disappears and the image sits
                    // on the wall as a decal (its PNG transparency preserved).
                    SetBoardVisible(mesh.transform, false);
                }
                var quad = FindOrCreateQuad(mesh.transform);
                quad.transform.localScale = new Vector3(w, h, 1f);
                SetQuadUv(quad, u0, v0, uSize, vSize);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.material.mainTexture = _tex;
                renderer.enabled = true;
            }
        }

        private static void SetQuadUv(GameObject _quad, float _u0, float _v0, float _uSize, float _vSize)
        {
            var filter = _quad.GetComponent<MeshFilter>();
            var mesh = filter.mesh; // per-quad instance; never write the shared primitive mesh
            var uv = new Vector2[baseUv.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2(_u0 + baseUv[i].x * _uSize, _v0 + baseUv[i].y * _vSize);
            }
            mesh.uv = uv;
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
                SetBoardVisible(mesh.transform, true); // graffiti ghost board comes back
                var old = mesh.transform.Find(QuadName);
                if (old != null)
                {
                    Object.Destroy(old.gameObject);
                }
            }
        }

        private static void SetBoardVisible(Transform _anchor, bool _visible)
        {
            var board = _anchor.parent != null ? _anchor.parent.Find("Board") : null;
            var renderer = board != null ? board.GetComponent<MeshRenderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = _visible;
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
            if (baseUv == null)
            {
                // The primitive's pristine UVs, captured BEFORE anything touches
                // .mesh (which would swap sharedMesh for the instance).
                baseUv = quad.GetComponent<MeshFilter>().sharedMesh.uv;
            }
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

            // Prefer a clone of the board's own LIT material (Standard, shipped
            // in our bundle) so the image shades with the world instead of
            // glowing like a lightbox at night. Unlit fallback for safety.
            var board = _anchor.parent != null ? _anchor.parent.Find("Board") : null;
            var boardRenderer = board != null ? board.GetComponent<MeshRenderer>() : null;
            if (boardRenderer != null && boardRenderer.sharedMaterial != null)
            {
                var mat = new Material(boardRenderer.sharedMaterial);
                mat.color = Color.white; // the board's dark tint would crush the image
                renderer.material = mat;
            }
            else
            {
                renderer.material = new Material(PosterShader());
            }
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
