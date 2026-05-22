using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Provides access to baseline sprite colours so visual effects can restore them correctly.
    /// </summary>
    public interface IOriginalSpriteColorProvider
    {
        bool TryGetOriginalSpriteColor(SpriteRenderer renderer, out Color color);
    }
}



