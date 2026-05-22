using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
// DestructibleTileDatabase.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName="Destruction/Destructible Tile Database")]
[MovedFrom(true, null, null, "DestructibleTileDatabase")]
public class DestructibleTileDatabase : ScriptableObject
{
    public List<DestructibleTileData> entries = new();
    Dictionary<TileBase, DestructibleTileData> _map;

    public void Build()
    {
        _map = new Dictionary<TileBase, DestructibleTileData>();
        foreach (var e in entries)
            if (e && e.sourceTile && !_map.ContainsKey(e.sourceTile))
                _map.Add(e.sourceTile, e);
    }

    public bool TryGet(TileBase tile, out DestructibleTileData data)
    {
        if (_map == null) Build();
        return _map.TryGetValue(tile, out data);
    }
}



}




