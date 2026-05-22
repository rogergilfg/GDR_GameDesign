using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SmallScale.FantasyKingdomTileset.Building;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Dynamic HUD panel that displays all resour ces defined in the ResourceDatabase via the
/// DynamicResourceManager. Spawns rows at runtime based on available definitions.
/// </summary>
[DisallowMultipleComponent]
public class DynamicResourceHudUI : MonoBehaviour
{
    [System.Serializable]
    private class Row
    {
        public Image icon;
        public TMP_Text amount;
        public ResourceTypeDef type;
    }

    [Header("Templates / Root")]
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private Image iconTemplate;
    [SerializeField] private TMP_Text amountTemplate;

    [Header("Visibility")]
    [Tooltip("Only show the resource panel when relevant menus (build/inventory) are open.")]
    [SerializeField] private bool showOnlyDuringMenus = true;
    [Tooltip("Inventory UI whose visibility toggles the resource panel. When left empty the controller will be discovered automatically.")]
    [SerializeField] private InventoryUIController inventoryUI;
    [Tooltip("Automatically search the scene for an InventoryUIController when none is assigned.")]
    [SerializeField] private bool autoFindInventoryUI = true;
    [Tooltip("CanvasGroup used to show/hide the panel without disabling this component. One will be added automatically if omitted.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private readonly List<Row> rows = new List<Row>();
    private readonly Dictionary<ResourceTypeDef, Row> rowsByType = new Dictionary<ResourceTypeDef, Row>();
    private DynamicResourceManager subscribed;
    private bool currentPanelVisible = true;
    float inventorySearchCooldown = 0f;

    private void Awake()
    {
        ResolveCanvasGroup();
        EnsureTemplatesInactive();
        RebuildRows();
        TryResolveInventoryUI();
        UpdatePanelVisibility(force: true);
    }

    private void OnEnable()
    {
        TrySubscribe();
        UpdatePanelVisibility(force: true);
    }

    private void OnDisable()
    {
        if (subscribed != null)
        {
            subscribed.OnResourcesUpdated -= HandleResourcesUpdated;
            subscribed = null;
        }
    }

    private void Update()
    {
        if (subscribed == null)
        {
            TrySubscribe();
        }

        if (autoFindInventoryUI && (!inventoryUI || inventoryUI.Equals(null)))
        {
            inventorySearchCooldown -= Time.unscaledDeltaTime;
            if (inventorySearchCooldown <= 0f)
            {
                inventorySearchCooldown = 1f;
                TryResolveInventoryUI();
            }
        }

        UpdatePanelVisibility();
    }

    private void TrySubscribe()
    {
        var dyn = DynamicResourceManager.Instance;
        if (dyn == null || dyn.Database == null) return;

        if (subscribed != null)
        {
            if (subscribed != dyn)
            {
                subscribed.OnResourcesUpdated -= HandleResourcesUpdated;
                subscribed = null;
            }
        }

        if (subscribed == null)
        {
            subscribed = dyn;
            subscribed.OnResourcesUpdated += HandleResourcesUpdated;
            RebuildRows();
            HandleResourcesUpdated(subscribed.CurrentResources);
        }
    }

    private void EnsureTemplatesInactive()
    {
        if (iconTemplate != null) iconTemplate.gameObject.SetActive(false);
        if (amountTemplate != null) amountTemplate.gameObject.SetActive(false);
    }

    private void RebuildRows()
    {
        // clear existing
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r != null)
            {
                if (r.icon != null) Destroy(r.icon.gameObject);
                if (r.amount != null) Destroy(r.amount.gameObject);
            }
        }
        rows.Clear();
        rowsByType.Clear();
        // defer creation to HandleResourcesUpdated so only non-zero entries are shown
    }

    private void HandleResourcesUpdated(ResourceSet set)
    {
        var dyn = DynamicResourceManager.Instance;
        if (dyn == null || dyn.Database == null || rowsRoot == null || iconTemplate == null || amountTemplate == null)
        {
            return;
        }

        if (!currentPanelVisible && showOnlyDuringMenus)
        {
            return;
        }

        // Track which types should be displayed this frame
        var keep = new HashSet<ResourceTypeDef>();

        foreach (var def in dyn.Database.Resources)
        {
            if (def == null) continue;
            int value = set != null ? set.Get(def) : 0;
            // Show if player currently has some OR has ever unlocked/seen this currency
            bool unlocked = dyn.IsUnlocked(def);
            if (value > 0 || unlocked)
            {
                keep.Add(def);
                if (!rowsByType.TryGetValue(def, out Row row) || row == null)
                {
                    row = CreateRow(def);
                }

                if (row.amount != null)
                {
                    row.amount.text = value.ToString();
                }
            }
        }

        // Remove rows no longer needed (not unlocked and amount back to 0)
        var toRemove = new List<ResourceTypeDef>();
        foreach (var kvp in rowsByType)
        {
            if (!keep.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveRow(toRemove[i]);
        }
    }

    private Row CreateRow(ResourceTypeDef def)
    {
        var icon = Instantiate(iconTemplate, rowsRoot);
        icon.gameObject.SetActive(true);
        icon.sprite = def.Icon;
        icon.enabled = def.Icon != null;

        var label = Instantiate(amountTemplate, rowsRoot);
        label.gameObject.SetActive(true);
        label.text = "0";

        var row = new Row { icon = icon, amount = label, type = def };
        rows.Add(row);
        rowsByType[def] = row;
        icon.rectTransform.SetAsLastSibling();
        label.rectTransform.SetAsLastSibling();
        return row;
    }

    private void RemoveRow(ResourceTypeDef def)
    {
        if (!rowsByType.TryGetValue(def, out Row row) || row == null)
        {
            return;
        }

        if (row.icon != null) Destroy(row.icon.gameObject);
        if (row.amount != null) Destroy(row.amount.gameObject);
        rows.Remove(row);
        rowsByType.Remove(def);
    }

    void ResolveCanvasGroup()
    {
        if (!panelCanvasGroup)
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (!panelCanvasGroup)
        {
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (panelCanvasGroup)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
    }

    void TryResolveInventoryUI()
    {
        if (!autoFindInventoryUI || inventoryUI) return;

        var found = Resources.FindObjectsOfTypeAll<InventoryUIController>();
        for (int i = 0; i < found.Length; i++)
        {
            var candidate = found[i];
            if (!candidate || candidate.hideFlags != HideFlags.None) continue;
            if (!candidate.gameObject.scene.IsValid()) continue;
            inventoryUI = candidate;
            break;
        }
    }

    void UpdatePanelVisibility(bool force = false)
    {
        bool shouldShow = !showOnlyDuringMenus || ShouldPanelBeVisible();
        if (!force && shouldShow == currentPanelVisible)
        {
            return;
        }

        SetPanelVisible(shouldShow);
    }

    bool ShouldPanelBeVisible()
    {
        bool buildMenuOpen = BuildMenuController.IsAnyMenuOpen;
        bool inventoryOpen = inventoryUI && !inventoryUI.Equals(null) && inventoryUI.IsInventoryVisible();
        return buildMenuOpen || inventoryOpen;
    }

    void SetPanelVisible(bool visible)
    {
        currentPanelVisible = visible;

        if (panelCanvasGroup)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }

        if (visible && subscribed != null)
        {
            HandleResourcesUpdated(subscribed.CurrentResources);
        }
    }
}
}









