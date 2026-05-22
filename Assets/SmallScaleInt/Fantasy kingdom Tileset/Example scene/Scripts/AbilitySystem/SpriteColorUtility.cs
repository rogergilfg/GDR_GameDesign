using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    internal static class SpriteColorUtility
    {
        public static bool TryGetOriginalColor(Transform source, SpriteRenderer renderer, out Color color)
        {
            color = default;
            if (!renderer)
            {
                return false;
            }

            if (!source)
            {
                return false;
            }

            var provider = source.GetComponentInParent<IOriginalSpriteColorProvider>();
            if (provider != null && provider.TryGetOriginalSpriteColor(renderer, out color))
            {
                return true;
            }

            return false;
        }
    }
}



