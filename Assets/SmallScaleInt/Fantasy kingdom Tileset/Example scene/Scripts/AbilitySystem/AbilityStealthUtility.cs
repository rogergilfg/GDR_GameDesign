using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Tracks actors that are temporarily hidden from EnemyAI threat detection (e.g., via invisibility).
    /// </summary>
    public static class AbilityStealthUtility
    {
        static readonly Dictionary<Transform, int> ActiveRoots = new();

        public static void Register(Transform root)
        {
            if (ReferenceEquals(root, null)) return;
            if (ActiveRoots.TryGetValue(root, out int count))
            {
                ActiveRoots[root] = count + 1;
            }
            else
            {
                ActiveRoots.Add(root, 1);
            }
        }

        public static void Unregister(Transform root)
        {
            if (ReferenceEquals(root, null)) return;
            if (!ActiveRoots.TryGetValue(root, out int count))
                return;

            count--;
            if (count <= 0)
            {
                ActiveRoots.Remove(root);
            }
            else
            {
                ActiveRoots[root] = count;
            }
        }

        public static bool IsInvisible(Transform candidate)
        {
            if (!candidate || ActiveRoots.Count == 0) return false;
            Transform current = candidate;
            while (current)
            {
                if (ActiveRoots.ContainsKey(current))
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}




