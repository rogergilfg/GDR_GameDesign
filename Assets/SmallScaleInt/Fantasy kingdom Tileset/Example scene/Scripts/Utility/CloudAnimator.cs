using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and animates parallax clouds that drift across the scene with smooth fade in/out.
/// </summary>
[MovedFrom(true, null, null, "CloudAnimator")]
public class CloudAnimator : MonoBehaviour
{
    private sealed class CloudInstance
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float speed;
        public float elapsed;
        public float lifetime;
        public float fadeInDuration;
        public float fadeOutDuration;
        public float maxAlpha;
        public float bobAmplitude;
        public float bobFrequency;
        public float bobPhase;
        public float rotationAmplitude;
        public float rotationFrequency;
        public float rotationPhase;
        public float baseScale;
        public float scalePulseAmplitude;
        public float scalePulseFrequency;
        public float scalePulsePhase;
    }

    [Header("Cloud Assets")]
    [SerializeField]
    [Tooltip("Sprites that will be randomly selected for the spawned clouds.")]
    private Sprite[] cloudSprites;

    [SerializeField]
    [Tooltip("Optional material override applied to generated cloud renderers.")]
    private Material cloudMaterial;

    [Header("Spawn Settings")]
    [SerializeField]
    [Tooltip("Number of clouds spawned immediately on start to avoid an empty sky.")]
    private int initialCloudCount = 3;

    [SerializeField]
    [Tooltip("Minimum and maximum delay (in seconds) between automatic cloud spawns.")]
    private Vector2 spawnIntervalRange = new Vector2(10f, 18f);

    [SerializeField]
    [Tooltip("Local-space size of the region that clouds will traverse.")]
    private Vector2 areaSize = new Vector2(60f, 30f);

    [SerializeField]
    [Tooltip("Additional random vertical offset applied to the cloud end point.")]
    private Vector2 verticalDriftRange = new Vector2(-3f, 3f);

    [Header("Cloud Appearance")]
    [SerializeField]
    [Tooltip("Range of movement speed (in units per second) applied to spawned clouds.")]
    private Vector2 speedRange = new Vector2(1.2f, 2.2f);

    [SerializeField]
    [Tooltip("Range of uniform scale multipliers applied to spawned clouds.")]
    private Vector2 scaleRange = new Vector2(0.8f, 1.4f);

    [SerializeField]
    [Tooltip("Range for the maximum opacity that clouds can reach.")]
    private Vector2 alphaRange = new Vector2(0.55f, 0.9f);

    [SerializeField]
    [Tooltip("Range for the fade-in and fade-out durations in seconds.")]
    private Vector2 fadeDurationRange = new Vector2(2.5f, 4.5f);

    [SerializeField]
    [Tooltip("Sorting layer assigned to all generated cloud renderers.")]
    private string sortingLayerName = "Default";

    [SerializeField]
    [Tooltip("Sorting order assigned to all generated cloud renderers.")]
    private int sortingOrder = 150;

    [SerializeField]
    [Tooltip("Optional jitter applied to the local z position to give subtle depth variation.")]
    private Vector2 depthOffsetRange = new Vector2(-0.05f, 0.05f);

    [Header("Secondary Motion")]
    [SerializeField]
    [Tooltip("Vertical bob amplitude range applied to drifting clouds.")]
    private Vector2 bobAmplitudeRange = new Vector2(0.1f, 0.35f);

    [SerializeField]
    [Tooltip("Frequency range (in Hz) for the vertical bob motion.")]
    private Vector2 bobFrequencyRange = new Vector2(0.05f, 0.15f);

    [SerializeField]
    [Tooltip("Range (in degrees) for the subtle rotation applied to clouds.")]
    private Vector2 rotationAmplitudeRange = new Vector2(0.5f, 2.5f);

    [SerializeField]
    [Tooltip("Frequency range (in Hz) for the gentle rotational drift.")]
    private Vector2 rotationFrequencyRange = new Vector2(0.02f, 0.08f);

    [SerializeField]
    [Tooltip("Range for additional scale pulsing around the base cloud scale.")]
    private Vector2 scalePulseAmplitudeRange = new Vector2(0.0f, 0.05f);

    [SerializeField]
    [Tooltip("Frequency range (in Hz) for the subtle scale pulsing effect.")]
    private Vector2 scalePulseFrequencyRange = new Vector2(0.02f, 0.12f);

    [Header("Behaviour")]
    [SerializeField]
    [Tooltip("Whether clouds should continue to spawn automatically.")]
    private bool autoSpawn = true;

    private readonly List<CloudInstance> activeClouds = new List<CloudInstance>();
    private readonly Stack<CloudInstance> cloudPool = new Stack<CloudInstance>();
    private float nextSpawnTime;

    private void OnEnable()
    {
        PrepareInitialClouds();
        ScheduleNextSpawn(Time.time);
    }

    private void OnDisable()
    {
        ClearClouds();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        UpdateClouds(deltaTime);

        if (!autoSpawn || cloudSprites == null || cloudSprites.Length == 0)
        {
            return;
        }

        if (Time.time >= nextSpawnTime)
        {
            SpawnCloud();
            ScheduleNextSpawn(Time.time);
        }
    }

    private void PrepareInitialClouds()
    {
        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            return;
        }

        for (int i = 0; i < initialCloudCount; i++)
        {
            CloudInstance instance = SpawnCloud();
            if (instance != null)
            {
                instance.elapsed = Random.Range(0f, instance.lifetime * 0.75f);
                ApplyCloudState(instance);
            }
        }
    }

    private void ScheduleNextSpawn(float currentTime)
    {
        if (!autoSpawn)
        {
            return;
        }

        float interval = Mathf.Max(0.1f, GetRandomInRange(spawnIntervalRange));
        nextSpawnTime = currentTime + interval;
    }

    private CloudInstance SpawnCloud()
    {
        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            return null;
        }

        Sprite sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
        if (sprite == null)
        {
            return null;
        }

        CloudInstance instance = GetCloudInstance();
        if (instance.transform == null)
        {
            GameObject cloudObject = new GameObject("Cloud");
            cloudObject.transform.SetParent(transform, false);

            SpriteRenderer renderer = cloudObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (cloudMaterial != null)
            {
                renderer.material = cloudMaterial;
            }

            instance.transform = cloudObject.transform;
            instance.renderer = renderer;
        }

        instance.transform.gameObject.SetActive(true);

        ConfigureCloudInstance(instance, sprite);
        activeClouds.Add(instance);
        ApplyCloudState(instance);
        return instance;
    }

    private CloudInstance GetCloudInstance()
    {
        return cloudPool.Count > 0 ? cloudPool.Pop() : new CloudInstance();
    }

    private void ConfigureCloudInstance(CloudInstance instance, Sprite sprite)
    {
        float halfWidth = Mathf.Abs(areaSize.x) * 0.5f;
        float halfHeight = Mathf.Abs(areaSize.y) * 0.5f;

        bool spawnFromLeft = Random.value > 0.5f;
        float startX = spawnFromLeft ? -halfWidth : halfWidth;
        float endX = spawnFromLeft ? halfWidth : -halfWidth;

        float baseY = Random.Range(-halfHeight, halfHeight);
        float endY = baseY + GetRandomInRange(verticalDriftRange);

        Vector3 startLocal = new Vector3(startX, baseY, 0f);
        Vector3 endLocal = new Vector3(endX, endY, 0f);

        Vector3 startWorld = transform.TransformPoint(startLocal);
        Vector3 endWorld = transform.TransformPoint(endLocal);

        float speed = Mathf.Max(0.01f, GetRandomInRange(speedRange));
        float distance = Vector3.Distance(startWorld, endWorld);
        float lifetime = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, speed));

        float fadeDuration = Mathf.Max(0.1f, GetRandomInRange(fadeDurationRange));
        float fadeIn = Mathf.Clamp(fadeDuration, 0.1f, lifetime * 0.45f);
        float fadeOut = Mathf.Clamp(fadeDuration, 0.1f, lifetime * 0.45f);

        float scale = Mathf.Max(0.01f, GetRandomInRange(scaleRange));
        float alpha = Mathf.Clamp01(GetRandomInRange(alphaRange));
        float depthOffset = GetRandomInRange(depthOffsetRange);

        float bobAmplitude = Mathf.Max(0f, GetRandomInRange(bobAmplitudeRange));
        float bobFrequency = Mathf.Max(0f, GetRandomInRange(bobFrequencyRange)) * Mathf.PI * 2f;
        float rotationAmplitude = Mathf.Abs(GetRandomInRange(rotationAmplitudeRange));
        float rotationFrequency = Mathf.Max(0f, GetRandomInRange(rotationFrequencyRange)) * Mathf.PI * 2f;
        float scalePulseAmplitude = Mathf.Max(0f, GetRandomInRange(scalePulseAmplitudeRange));
        float scalePulseFrequency = Mathf.Max(0f, GetRandomInRange(scalePulseFrequencyRange)) * Mathf.PI * 2f;

        startWorld.z += depthOffset;
        endWorld.z += depthOffset;

        instance.startPosition = startWorld;
        instance.endPosition = endWorld;
        instance.speed = speed;
        instance.elapsed = 0f;
        instance.lifetime = lifetime;
        instance.fadeInDuration = fadeIn;
        instance.fadeOutDuration = fadeOut;
        instance.maxAlpha = Mathf.Clamp01(alpha);
        instance.bobAmplitude = bobAmplitude;
        instance.bobFrequency = bobFrequency;
        instance.bobPhase = Random.Range(0f, Mathf.PI * 2f);
        instance.rotationAmplitude = rotationAmplitude * (Random.value > 0.5f ? -1f : 1f);
        instance.rotationFrequency = rotationFrequency;
        instance.rotationPhase = Random.Range(0f, Mathf.PI * 2f);
        instance.baseScale = scale;
        instance.scalePulseAmplitude = scalePulseAmplitude;
        instance.scalePulseFrequency = scalePulseFrequency;
        instance.scalePulsePhase = Random.Range(0f, Mathf.PI * 2f);

        instance.transform.localScale = Vector3.one * scale;
        instance.transform.position = startWorld;
        instance.renderer.sprite = sprite;
        instance.renderer.color = new Color(1f, 1f, 1f, 0f);
        instance.transform.rotation = Quaternion.identity;
    }

    private void UpdateClouds(float deltaTime)
    {
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            CloudInstance instance = activeClouds[i];
            instance.elapsed += deltaTime;

            if (instance.elapsed >= instance.lifetime)
            {
                RecycleCloud(i);
                continue;
            }

            ApplyCloudState(instance);
        }
    }

    private void ApplyCloudState(CloudInstance instance)
    {
        float t = Mathf.Clamp01(instance.elapsed / instance.lifetime);
        float easedT = Mathf.SmoothStep(0f, 1f, t);
        Vector3 position = Vector3.Lerp(instance.startPosition, instance.endPosition, easedT);

        if (instance.bobAmplitude > 0f && instance.bobFrequency > 0f)
        {
            float bobOffset = Mathf.Sin(instance.elapsed * instance.bobFrequency + instance.bobPhase) * instance.bobAmplitude;
            position.y += bobOffset;
        }

        instance.transform.position = position;

        float scale = instance.baseScale;
        if (instance.scalePulseAmplitude > 0f && instance.scalePulseFrequency > 0f)
        {
            float pulse = Mathf.Sin(instance.elapsed * instance.scalePulseFrequency + instance.scalePulsePhase) * instance.scalePulseAmplitude;
            scale += pulse;
        }

        instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (Mathf.Abs(instance.rotationAmplitude) > 0f && instance.rotationFrequency > 0f)
        {
            float rotation = Mathf.Sin(instance.elapsed * instance.rotationFrequency + instance.rotationPhase) * instance.rotationAmplitude;
            instance.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }

        float alpha = EvaluateAlpha(instance);
        Color color = instance.renderer.color;
        color.a = alpha;
        instance.renderer.color = color;
    }

    private float EvaluateAlpha(CloudInstance instance)
    {
        float alpha = instance.maxAlpha;

        if (instance.elapsed < instance.fadeInDuration)
        {
            float normalized = Mathf.Clamp01(instance.elapsed / instance.fadeInDuration);
            alpha *= Mathf.SmoothStep(0f, 1f, normalized);
        }
        else if (instance.elapsed > instance.lifetime - instance.fadeOutDuration)
        {
            float timeRemaining = instance.lifetime - instance.elapsed;
            float normalized = Mathf.Clamp01(timeRemaining / instance.fadeOutDuration);
            alpha *= Mathf.SmoothStep(0f, 1f, normalized);
        }

        return alpha;
    }

    private void RecycleCloud(int index)
    {
        CloudInstance instance = activeClouds[index];
        activeClouds.RemoveAt(index);

        if (instance.transform != null)
        {
            instance.transform.gameObject.SetActive(false);
            if (instance.renderer != null)
            {
                instance.renderer.sprite = null;
                instance.renderer.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        instance.elapsed = 0f;
        instance.lifetime = 0f;
        instance.fadeInDuration = 0f;
        instance.fadeOutDuration = 0f;
        instance.maxAlpha = 0f;
        instance.bobAmplitude = 0f;
        instance.bobFrequency = 0f;
        instance.bobPhase = 0f;
        instance.scalePulseAmplitude = 0f;
        instance.scalePulseFrequency = 0f;
        instance.scalePulsePhase = 0f;
        instance.rotationAmplitude = 0f;
        instance.rotationFrequency = 0f;
        instance.rotationPhase = 0f;
        instance.baseScale = 0f;

        cloudPool.Push(instance);
    }

    private void ClearClouds()
    {
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            CloudInstance instance = activeClouds[i];
            if (instance.transform != null)
            {
                Destroy(instance.transform.gameObject);
            }
            instance.transform = null;
            instance.renderer = null;
        }

        activeClouds.Clear();

        foreach (CloudInstance instance in cloudPool)
        {
            if (instance.transform != null)
            {
                Destroy(instance.transform.gameObject);
            }

            instance.transform = null;
            instance.renderer = null;
        }

        cloudPool.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 0.85f, 1f, 0.35f);

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, new Vector3(Mathf.Abs(areaSize.x), Mathf.Abs(areaSize.y), 0f));
        Gizmos.matrix = previous;
    }

    /// <summary>
    /// Spawns a new cloud immediately. Useful when triggering clouds manually from other scripts or events.
    /// </summary>
    public void SpawnCloudManual()
    {
        SpawnCloud();
    }

    private static float GetRandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}



}





