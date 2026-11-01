using System.Collections.Generic;
using UnityEngine;

namespace KitsunePosters
{
    public struct PosterLayout
    {
        public bool Tiled;       // part of a complete rectangle bigger than 1x1
        public int Cols, Rows;   // rectangle size in blocks
        public int Col, Row;     // this block's cell, col 0 = U origin, row 0 = bottom
    }

    /// <summary>
    /// Adjacent poster blocks (same block, same facing, same URL) forming a
    /// complete rectangle show ONE image spread across them. Every member
    /// computes the same group from world state, so no extra sync is needed.
    /// </summary>
    public static class PosterTiling
    {
        public const int MaxGroupCells = 64;

        public static PosterLayout Single => new PosterLayout { Tiled = false, Cols = 1, Rows = 1, Col = 0, Row = 0 };

        /// <summary>
        /// One block step along the image's +U (its right edge, as the viewer
        /// sees it) once PosterSurface has turned it upright, or false when the
        /// board isn't squarely on a wall (floor, ceiling, 45 degrees). Worked
        /// out from the same rotation the game renders the block with, so every
        /// placement that LOOKS alike tiles alike, upside-down and sideways
        /// ones included.
        /// </summary>
        public static bool TryGetUStep(BlockValue _bv, out Vector3i _step)
        {
            _step = Vector3i.zero;
            var shape = _bv.Block?.shape;
            if (shape == null)
            {
                return false;
            }
            Quaternion image = shape.GetRotation(_bv) * Quaternion.AngleAxis(PosterSurface.UprightRoll(_bv), Vector3.forward);
            if ((image * Vector3.up).y < 0.99f)
            {
                return false; // lying flat: no upright image to tile
            }
            Vector3 u = image * Vector3.right;
            if (Mathf.Abs(u.x) < 0.99f && Mathf.Abs(u.z) < 0.99f)
            {
                return false; // 45 degrees: neighbours aren't in its plane
            }
            _step = new Vector3i(Mathf.RoundToInt(u.x), 0, Mathf.RoundToInt(u.z));
            return true;
        }

        public static PosterLayout Compute(World _world, Vector3i _pos)
        {
            var layout = Single;
            if (_world == null)
            {
                return layout;
            }
            BlockValue bv = _world.GetBlock(_pos);
            string url = UrlAt(_world, _pos);
            // Multi-block posters don't tile: their cells are not 1m apart.
            // NB isMultiBlock is true for ANY block that declares MultiBlockDim,
            // "1,1,1" included (playerSignWood1x1 does), so check the dims.
            if (url == null || IsBiggerThanOneBlock(bv.Block) || OptsOutOfTiling(bv.Block) || !TryGetUStep(bv, out Vector3i h))
            {
                return layout;
            }
            Vector3i v = new Vector3i(0, 1, 0);

            // Flood fill in the poster's plane over matching blocks.
            var seen = new HashSet<Vector3i> { _pos };
            var frontier = new List<Vector3i> { _pos };
            var steps = new[] { h, h * -1, v, v * -1 };
            while (frontier.Count > 0 && seen.Count <= MaxGroupCells)
            {
                var p = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                foreach (var step in steps)
                {
                    var n = p + step;
                    if (!seen.Contains(n) && Matches(_world, n, bv, url))
                    {
                        seen.Add(n);
                        frontier.Add(n);
                    }
                }
            }
            if (seen.Count <= 1 || seen.Count > MaxGroupCells)
            {
                return layout;
            }

            // Bounding box in (a, b) plane coordinates relative to this block.
            int aMin = 0, aMax = 0, bMin = 0, bMax = 0;
            foreach (var p in seen)
            {
                var d = p - _pos;
                int a = d.x * h.x + d.z * h.z; // signed steps along U+
                int b = d.y;
                if (a < aMin) aMin = a;
                if (a > aMax) aMax = a;
                if (b < bMin) bMin = b;
                if (b > bMax) bMax = b;
            }
            int cols = aMax - aMin + 1;
            int rows = bMax - bMin + 1;

            // Only a COMPLETE rectangle tiles; an L-shape falls back to
            // every poster showing the whole image by itself.
            if (seen.Count != cols * rows)
            {
                return layout;
            }
            layout.Tiled = true;
            layout.Cols = cols;
            layout.Rows = rows;
            layout.Col = -aMin;
            layout.Row = -bMin;
            return layout;
        }

        /// <summary>World positions of the group's members, this block included.</summary>
        public static List<Vector3i> Members(World _world, Vector3i _pos, PosterLayout _layout)
        {
            var members = new List<Vector3i>();
            if (!TryGetUStep(_world.GetBlock(_pos), out Vector3i h))
            {
                return members;
            }
            for (int a = 0; a < _layout.Cols; a++)
            {
                for (int b = 0; b < _layout.Rows; b++)
                {
                    members.Add(_pos + h * (a - _layout.Col) + new Vector3i(0, b - _layout.Row, 0));
                }
            }
            return members;
        }

        /// <summary>In-plane neighbours that are posters, for re-render nudges after a clear.</summary>
        public static List<Vector3i> PosterNeighbours(World _world, Vector3i _pos)
        {
            var result = new List<Vector3i>();
            if (_world == null || !TryGetUStep(_world.GetBlock(_pos), out Vector3i h))
            {
                return result;
            }
            Vector3i v = new Vector3i(0, 1, 0);
            foreach (var step in new[] { h, h * -1, v, v * -1 })
            {
                var n = _pos + step;
                if (SignableAt(_world, n) != null && IsPosterBlock(_world.GetBlock(n)))
                {
                    result.Add(n);
                }
            }
            return result;
        }

        private static bool Matches(World _world, Vector3i _pos, BlockValue _like, string _url)
        {
            BlockValue bv = _world.GetBlock(_pos);
            // Same U step means the same facing, so the same wall.
            if (bv.type != _like.type || !TryGetUStep(bv, out Vector3i step) || !TryGetUStep(_like, out Vector3i likeStep) || step != likeStep)
            {
                return false;
            }
            return UrlAt(_world, _pos) == _url;
        }

        public static bool IsBiggerThanOneBlock(Block _block)
        {
            if (_block == null)
            {
                return true;
            }
            if (!_block.isMultiBlock)
            {
                return false;
            }
            var dim = _block.multiBlockPos.dim;
            return dim.x != 1 || dim.y != 1 || dim.z != 1;
        }

        /// <summary>
        /// Tiled cells fill the whole 1m face, so a board smaller than its
        /// block (the small 2:3) sets KitsuneTiling false in blocks.xml.
        /// </summary>
        public static bool OptsOutOfTiling(Block _block)
        {
            var props = _block.Properties;
            return props.Values.ContainsKey("KitsuneTiling") && !props.GetBool("KitsuneTiling");
        }

        public static bool IsPosterBlock(BlockValue _bv)
        {
            return PosterBlocks.IsPoster(_bv.Block?.GetBlockName());
        }

        public static TEFeatureSignable SignableAt(World _world, Vector3i _pos)
        {
            return (_world.GetTileEntity(_pos) as TileEntityComposite)?.GetFeature<TEFeatureSignable>();
        }

        public static string UrlAt(World _world, Vector3i _pos)
        {
            var signable = SignableAt(_world, _pos);
            return signable == null ? null : PosterUrl.Extract(signable.signText?.Text);
        }
    }
}
