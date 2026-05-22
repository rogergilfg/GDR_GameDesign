using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[MovedFrom(true, null, null, "ProgrammaticTilemapShadows")]
public class ProgrammaticTilemapShadows : MonoBehaviour
{
    [Header("Source")]
    public Tilemap walls; // source tilemap

    [Header("Shadow Layers (top to bottom)")]
    public Tilemap[] shadowLayers;

    [Header("Projection (west-falling)")]
    [Range(0f, 1f)] public float squashY = 0.28f;  // 0.25â€“0.35 looks good
    public float skewXByY = -0.55f;                // negative = skew left (west)
    public Vector2 baseOffset = new Vector2(-0.35f, -0.10f);

    [Header("Pre-rotation")]
    [Tooltip("Rotate the sprite before projection (degrees). Try 90 or -90.")]
    public float rotationDegrees = 90f;

    [Header("Softness & Color")]
    public Vector2 perLayerOffset = new Vector2(-0.02f, -0.01f);
    [Tooltip("Shadow color for the first (top) layer.")]
    public Color firstLayerColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("How much to reduce alpha per additional layer (0..1). 0.2 -> 50%, 30%, 10%")]
    [Range(0f, 1f)] public float alphaFalloff = 0.2f;

    [Header("Exclusions")]
    [Tooltip("If any of these substrings appear in the tile's name (or sprite name), skip shadow generation.")]
    public string[] exclusionKeywords;
    [Tooltip("Also check the rendered sprite's name for exclusions.")]
    public bool checkSpriteName = true;
    [Tooltip("When true, matching ignores letter case.")]
    public bool ignoreCase = true;

    [Header("Rebuild")]
    public bool rebuildOnStart = true;

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.delayCall += () => { if (this) RebuildShadows(); };
#endif
    }

    void Start()
    {
        if (rebuildOnStart) RebuildShadows();
    }

    public void RebuildShadows()
    {
        if (!walls || shadowLayers == null || shadowLayers.Length == 0) return;

        foreach (var tm in shadowLayers) if (tm) tm.ClearAllTiles();

        var bounds = walls.cellBounds;
        var tiles = walls.GetTilesBlock(bounds);

        // constant transforms
        Matrix4x4 rot   = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, rotationDegrees));
        Matrix4x4 scale = Matrix4x4.Scale(new Vector3(1f, Mathf.Max(0f, squashY), 1f));
        Matrix4x4 shear = Matrix4x4.identity; shear.m01 = skewXByY;

        for (int z = 0; z < bounds.size.z; z++)
        for (int y = 0; y < bounds.size.y; y++)
        for (int x = 0; x < bounds.size.x; x++)
        {
            int i = x + bounds.size.x * (y + bounds.size.y * z);
            var tile = tiles[i];
            if (tile == null) continue;

            var cell = new Vector3Int(x + bounds.x, y + bounds.y, z + bounds.z);

            // --- NEW: exclusion check ---
            if (IsExcluded(tile, cell)) continue;

            for (int layer = 0; layer < shadowLayers.Length; layer++)
            {
                var tm = shadowLayers[layer];
                if (!tm) continue;

                tm.SetTile(cell, tile);
                tm.SetTileFlags(cell, TileFlags.None);

                // layer tint
                float a = Mathf.Clamp01(firstLayerColor.a - alphaFalloff * layer);
                var layerCol = new Color(firstLayerColor.r, firstLayerColor.g, firstLayerColor.b, a);
                tm.SetColor(cell, layerCol);

                // per-layer offset and final matrix
                Vector2 layerOffset = baseOffset + perLayerOffset * layer;
                Matrix4x4 translate = Matrix4x4.Translate(new Vector3(layerOffset.x, layerOffset.y, 0f));
                Matrix4x4 M = translate * shear * scale * rot;

                tm.SetTransformMatrix(cell, M);
            }
        }

        foreach (var tm in shadowLayers) if (tm) tm.RefreshAllTiles();
    }

    bool IsExcluded(TileBase tile, Vector3Int cell)
    {
        if (exclusionKeywords == null || exclusionKeywords.Length == 0) return false;

        var comp = ignoreCase ? System.StringComparison.OrdinalIgnoreCase
                              : System.StringComparison.Ordinal;

        // 1) tile asset name
        string tileName = tile.name ?? string.Empty;
        if (ContainsAny(tileName, comp)) return true;

        // 2) optional: sprite name currently rendered at this cell
        if (checkSpriteName)
        {
            // Tilemap.GetSprite exists in modern Unity versions
            var sprite = walls.GetSprite(cell);
            if (sprite && ContainsAny(sprite.name ?? string.Empty, comp)) return true;
        }

        return false;
    }

    bool ContainsAny(string haystack, System.StringComparison comp)
    {
        if (string.IsNullOrEmpty(haystack)) return false;
        for (int k = 0; k < exclusionKeywords.Length; k++)
        {
            var needle = exclusionKeywords[k];
            if (string.IsNullOrEmpty(needle)) continue;
            if (haystack.IndexOf(needle, comp) >= 0) return true;
        }
        return false;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ProgrammaticTilemapShadows))]
[MovedFrom(true, null, null, "ProgrammaticTilemapShadowsEditor")]
public class ProgrammaticTilemapShadowsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Rebuild Shadows"))
        {
            var t = (ProgrammaticTilemapShadows)target;
            t.RebuildShadows();
            EditorUtility.SetDirty(t);
        }
    }
}
#endif



}




