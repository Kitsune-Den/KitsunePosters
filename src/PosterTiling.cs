using System.Collections.Generic;

namespace KitsunePosters
{
    public struct PosterLayout
    {
        public bool Tiled;       // part of a complete rectangle bigger than 1x1
        public int Cols, Rows;   // rectangle size in blocks
        public int Col, Row;     // this block's cell, col 0 = U origin, row 0 = bottom
    }

    /// <summary>
    /// Adjacent poster blocks (same block, same rotation, same URL) forming a
    /// complete rectangle show ONE image spread across them. Every member
    /// computes the same group from world state, so no extra sync is needed.
    /// </summary>
    public static class PosterTiling
    {
        public const int MaxGroupCells = 64;

        // U+ direction per Basic90 rotation, so the image follows the block's
        // facing. If tiled images come out reversed in game for some facings,
        // fix the signs HERE (single source of truth).
        private static readonly Vector3i[] UDir =
        {
            new Vector3i(1, 0, 0),
            new Vector3i(0, 0, 1),
            new Vector3i(-1, 0, 0),
            new Vector3i(0, 0, -1),
        };

        public static PosterLayout Single => new PosterLayout { Tiled = false, Cols = 1, Rows = 1, Col = 0, Row = 0 };

        public static PosterLayout Compute(World _world, Vector3i _pos)
        {
            var layout = Single;
            if (_world == null)
            {
                return layout;
            }
            BlockValue bv = _world.GetBlock(_pos);
            string url = UrlAt(_world, _pos);
            if (url == null || bv.rotation > 3)
            {
                return layout;
            }
            Vector3i h = UDir[bv.rotation];
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
            BlockValue bv = _world.GetBlock(_pos);
            if (bv.rotation > 3)
            {
                return members;
            }
            Vector3i h = UDir[bv.rotation];
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
            BlockValue bv = _world.GetBlock(_pos);
            if (_world == null || bv.rotation > 3)
            {
                return result;
            }
            Vector3i h = UDir[bv.rotation];
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
            if (bv.type != _like.type || bv.rotation != _like.rotation)
            {
                return false;
            }
            return UrlAt(_world, _pos) == _url;
        }

        public static bool IsPosterBlock(BlockValue _bv)
        {
            string name = _bv.Block?.GetBlockName();
            return name != null && name.StartsWith("kitsunePoster");
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
