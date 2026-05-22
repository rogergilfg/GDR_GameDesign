using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Controls the visibility of roof tilemaps when the player enters interior tiles.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "RoofVisibilityController")]
public class RoofVisibilityController : MonoBehaviour
{
    [Header("General")]
    [SerializeField]
    private bool effectEnabled = true;

    [Header("Detection")]
    [SerializeField]
    private Tilemap tileCheck;

    [SerializeField]
    private List<Tilemap> roofTilemaps = new();

    [Header("Fade Settings")]
    [SerializeField, Range(0f, 1f)]
    private float roofTransparency = 0.25f;

    [SerializeField]
    private float fadeSpeed = 6f;

    private readonly List<TilemapState> tilemapStates = new();
    private bool isInside;

    private void OnEnable()
    {
        CacheTilemapStates();
    }

    private void CacheTilemapStates()
    {
        tilemapStates.Clear();

        foreach (var tilemap in roofTilemaps)
        {
            if (tilemap == null)
            {
                continue;
            }

            tilemapStates.Add(new TilemapState
            {
                Tilemap = tilemap,
                OriginalColor = tilemap.color
            });
        }
    }

    public bool EffectEnabled
    {
        get => effectEnabled;
        set => effectEnabled = value;
    }

    private void Update()
    {
        var insideNow = effectEnabled && EvaluateInside();
        if (insideNow != isInside)
        {
            isInside = insideNow;
        }

        UpdateTilemapFade(Time.deltaTime);
    }

    private void OnDisable()
    {
        RestoreTilemapsImmediate();
    }

    private void OnValidate()
    {
        fadeSpeed = Mathf.Max(0f, fadeSpeed);
        roofTransparency = Mathf.Clamp01(roofTransparency);
    }

    private bool EvaluateInside()
    {
        if (tileCheck == null)
        {
            return false;
        }

        Vector3 worldPosition = transform.position;
        Vector3Int cellPosition = tileCheck.WorldToCell(worldPosition);
        return tileCheck.HasTile(cellPosition);
    }

    private void UpdateTilemapFade(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        foreach (var state in tilemapStates)
        {
            if (state.Tilemap == null)
            {
                continue;
            }

            var currentColor = state.Tilemap.color;
            var targetAlpha = isInside ? roofTransparency : state.OriginalColor.a;
            var targetColor = new Color(state.OriginalColor.r, state.OriginalColor.g, state.OriginalColor.b, targetAlpha);

            if (fadeSpeed <= 0f)
            {
                state.Tilemap.color = targetColor;
            }
            else
            {
                float t = Mathf.Clamp01(deltaTime * fadeSpeed);
                state.Tilemap.color = Color.Lerp(currentColor, targetColor, t);
            }
        }
    }

    private void RestoreTilemapsImmediate()
    {
        foreach (var state in tilemapStates)
        {
            if (state.Tilemap != null)
            {
                state.Tilemap.color = state.OriginalColor;
            }
        }
        isInside = false;
    }

    private class TilemapState
    {
        public Tilemap Tilemap;
        public Color OriginalColor;
    }
}



}




