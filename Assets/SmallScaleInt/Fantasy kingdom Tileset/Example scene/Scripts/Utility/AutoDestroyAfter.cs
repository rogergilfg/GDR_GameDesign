using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;

[MovedFrom(true, null, null, "AutoDestroyAfter")]
public class AutoDestroyAfter : MonoBehaviour
{
    public float seconds = 2f;

    void OnEnable()
    {
        if (seconds <= 0f) seconds = 0.1f;
        Destroy(gameObject, seconds);
    }
}


}




