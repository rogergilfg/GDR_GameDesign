using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;

[MovedFrom(true, null, null, "GodRayAnimator")]
public class GodRayAnimator : MonoBehaviour
{
    [Header("Source Sprite")]
    [SerializeField] private Sprite raySprite;
    [SerializeField, Range(0f, 1f)] private float spriteAlpha = 0.6f;

    [Header("Rays Configuration")]
    [SerializeField, Min(1)] private int rayCount = 6;
    [Tooltip("Distance the rays travel along their main direction.")]
    [SerializeField] private float travelDistance = 8f;
    [Tooltip("Spread of the rays perpendicular to their travel direction.")]
    [SerializeField] private float perpendicularSpread = 3f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.7f, 1.2f);
    [SerializeField] private Vector2 rotationRange = new Vector2(-12f, 12f);

    [Header("Motion")]
    [SerializeField] private Vector2 movementDirection = new Vector2(1f, 2f);
    [SerializeField] private float baseSpeed = 0.35f;
    [SerializeField] private Vector2 speedVariation = new Vector2(0.75f, 1.25f);
    [SerializeField] private float flickerStrength = 0.15f;
    [SerializeField] private float flickerSpeed = 0.6f;

    [Header("Fade")]
    [Tooltip("If enabled, rays fade out near both ends of their travel distance.")]
    [SerializeField] private bool fadeAtExtents = true;
    [Tooltip("Fraction of the travel distance used for fade in/out on each side.")]
    [SerializeField, Range(0f, 0.5f)] private float fadeEdgeFraction = 0.2f;

    private readonly List<RayInstance> rays = new();
    private Vector2 mainDirection = Vector2.up;
    private Vector2 perpendicularDirection = Vector2.right;

    private void Awake()
    {
        CacheDirections();
    }

    private void OnValidate()
    {
        if (scaleRange.y < scaleRange.x)
        {
            scaleRange.y = scaleRange.x;
        }

        if (speedVariation.y < speedVariation.x)
        {
            speedVariation.y = speedVariation.x;
        }

        fadeEdgeFraction = Mathf.Clamp(fadeEdgeFraction, 0f, 0.5f);

        CacheDirections();
    }

    private void CacheDirections()
    {
        if (movementDirection.sqrMagnitude < 0.0001f)
        {
            mainDirection = Vector2.up;
        }
        else
        {
            mainDirection = movementDirection.normalized;
        }

        perpendicularDirection = new Vector2(-mainDirection.y, mainDirection.x);
    }

    private void Start()
    {
        CreateRays();
    }

    private void CreateRays()
    {
        ClearExistingRays();

        if (raySprite == null)
        {
            Debug.LogWarning($"{nameof(GodRayAnimator)} on {name} requires a sprite reference to create rays.");
            return;
        }

        for (int i = 0; i < rayCount; i++)
        {
            RayInstance instance = CreateRayInstance(i);
            rays.Add(instance);
        }
    }

    private RayInstance CreateRayInstance(int index)
    {
        GameObject rayObject = new GameObject($"GodRay_{index}");
        rayObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = rayObject.AddComponent<SpriteRenderer>();
        renderer.sprite = raySprite;
        renderer.color = new Color(1f, 1f, 1f, spriteAlpha);
        renderer.sortingOrder = index;

        float offset = Random.Range(-perpendicularSpread * 0.5f, perpendicularSpread * 0.5f);
        float along = Random.Range(-travelDistance * 0.5f, travelDistance * 0.5f);
        float speedMultiplier = Random.Range(speedVariation.x, speedVariation.y);
        float scale = Random.Range(scaleRange.x, scaleRange.y);
        float rotation = Random.Range(rotationRange.x, rotationRange.y);
        float flickerOffset = Random.Range(0f, Mathf.PI * 2f);

        rayObject.transform.localScale = new Vector3(scale, scale, 1f);
        rayObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Vector3 position = ConvertToWorld(along, offset);
        rayObject.transform.localPosition = position;

        return new RayInstance(rayObject.transform, renderer, along, offset, speedMultiplier, flickerOffset);
    }

    private void ClearExistingRays()
    {
        foreach (RayInstance ray in rays)
        {
            if (ray.Transform != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(ray.Transform.gameObject);
                }
                else
                {
                    DestroyImmediate(ray.Transform.gameObject);
                }
            }
        }

        rays.Clear();
    }

    private void Update()
    {
        if (rays.Count == 0)
        {
            return;
        }

        float travelHalf = travelDistance * 0.5f;
        float speed = baseSpeed;
        float time = Time.time;

        foreach (RayInstance ray in rays)
        {
            ray.Along += speed * ray.SpeedMultiplier * Time.deltaTime;

            if (ray.Along > travelHalf)
            {
                ray.Along = -travelHalf;
            }

            Vector3 position = ConvertToWorld(ray.Along, ray.Offset);
            ray.Transform.localPosition = position;

            if (ray.Renderer != null)
            {
                float flicker = 1f + Mathf.Sin((time + ray.FlickerOffset) * flickerSpeed) * flickerStrength;
                float fade = CalculateFadeMultiplier(ray.Along, travelHalf);
                Color color = ray.Renderer.color;
                color.a = Mathf.Clamp01(spriteAlpha * flicker * fade);
                ray.Renderer.color = color;
            }
        }
    }

    private float CalculateFadeMultiplier(float along, float travelHalf)
    {
        if (!fadeAtExtents)
        {
            return 1f;
        }

        if (travelHalf <= 0f || fadeEdgeFraction <= 0f)
        {
            return 1f;
        }

        float normalized = Mathf.InverseLerp(-travelHalf, travelHalf, along);
        float edge = Mathf.Clamp(fadeEdgeFraction, 0f, 0.5f);
        if (edge <= 0f)
        {
            return 1f;
        }

        float fadeIn = Mathf.Clamp01(normalized / edge);
        float fadeOut = Mathf.Clamp01((1f - normalized) / edge);
        return Mathf.Min(fadeIn, fadeOut);
    }

    private Vector3 ConvertToWorld(float along, float offset)
    {
        Vector2 point = mainDirection * along + perpendicularDirection * offset;
        return new Vector3(point.x, point.y, 0f);
    }

    private sealed class RayInstance
    {
        public RayInstance(Transform transform, SpriteRenderer renderer, float along, float offset, float speedMultiplier, float flickerOffset)
        {
            Transform = transform;
            Renderer = renderer;
            Along = along;
            Offset = offset;
            SpeedMultiplier = speedMultiplier;
            FlickerOffset = flickerOffset;
        }

        public Transform Transform { get; }
        public SpriteRenderer Renderer { get; }
        public float Along { get; set; }
        public float Offset { get; }
        public float SpeedMultiplier { get; }
        public float FlickerOffset { get; }
    }
}



}






