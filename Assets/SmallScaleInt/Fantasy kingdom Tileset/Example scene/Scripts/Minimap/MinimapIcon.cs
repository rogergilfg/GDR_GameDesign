using UnityEngine;

namespace FantasyKingdoms.Minimap
{
    /// <summary>
    /// Legacy component kept so existing prefabs don't lose their minimap references now
    /// that icon rendering has been removed. The component simply requests that the
    /// minimap refresh itself when relevant objects appear or disappear.
    /// </summary>
    public class MinimapIcon : MonoBehaviour
    {
        private void OnEnable()
        {
            MinimapController.Instance?.RequestRefresh();
        }

        private void OnDisable()
        {
            MinimapController.Instance?.RequestRefresh();
        }
    }
}





