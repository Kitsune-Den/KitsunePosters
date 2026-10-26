using System.Collections.Generic;

namespace KitsunePosters
{
    /// <summary>
    /// The poster sign features seen this session, so the kposters on/off
    /// toggle can re-render loaded posters at once instead of waiting for
    /// their chunks to reload.
    /// </summary>
    public static class PosterRegistry
    {
        private static readonly HashSet<TEFeatureSignable> live = new HashSet<TEFeatureSignable>();

        public static void Track(TEFeatureSignable _feature)
        {
            live.Add(_feature);
        }

        public static void Reapply()
        {
            var dead = new List<TEFeatureSignable>();
            foreach (var feature in live)
            {
                var meshes = feature.smartTextMesh;
                if (meshes == null || meshes.Length == 0 || meshes[0] == null)
                {
                    dead.Add(feature); // its model went with the chunk
                    continue;
                }
                SignRefreshPatch.Postfix(feature);
            }
            foreach (var feature in dead)
            {
                live.Remove(feature);
            }
        }
    }
}
