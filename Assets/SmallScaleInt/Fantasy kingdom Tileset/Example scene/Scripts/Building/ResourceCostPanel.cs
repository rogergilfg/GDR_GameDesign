using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.Building
{
/// <summary>
/// Displays the resource cost for a build part using icons and numerical amounts.
/// </summary>
[MovedFrom(true, null, null, "ResourceCostPanel")]
public sealed class ResourceCostPanel : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Parent transform where icon and amount elements will be instantiated.")]
    private Transform contentRoot;

    [SerializeField]
    [Tooltip("Template image used when displaying a resource icon. The template is kept inactive and cloned at runtime.")]
    private Image iconTemplate;

    [SerializeField]
    [Tooltip("Template text element used when displaying a resource amount. The template is kept inactive and cloned at runtime.")]
    private TMP_Text amountTemplate;

    [SerializeField]
    [Tooltip("Text element used to display additional information about the hovered build part.")]
    private TMP_Text InfoTXT;

    private readonly List<GameObject> spawnedElements = new List<GameObject>();
    

    private void Awake()
    {
        EnsureTemplatesInactive();
    }

    private void OnValidate()
    {
        EnsureTemplatesInactive();
    }

    /// <summary>
    /// Displays the supplied resource cost inside the panel.
    /// </summary>
    /// <param name="bundle">The cost to display.</param>
    // Legacy bundle path removed; use ResourceSet overload

    /// <summary>
    /// Displays a dynamic resource set cost using icons on the resource type defs.
    /// </summary>
    public void DisplayCost(ResourceSet set, string infoText)
    {
        ClearCost();

        UpdateInfoText(infoText);

        if (set == null || set.IsEmpty || contentRoot == null || iconTemplate == null || amountTemplate == null)
        {
            return;
        }

        var list = set.Amounts;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.type == null || a.amount <= 0) continue;
            SpawnIcon(a.type != null ? a.type.Icon : null);
            SpawnAmount(a.amount);
        }
    }

    /// <summary>
    /// Removes any previously displayed cost elements from the panel.
    /// </summary>
    public void ClearCost()
    {
        for (int i = 0; i < spawnedElements.Count; i++)
        {
            GameObject element = spawnedElements[i];
            if (element != null)
            {
                Destroy(element);
            }
        }

        spawnedElements.Clear();

        UpdateInfoText(string.Empty);
    }

    private void UpdateInfoText(string infoText)
    {
        if (InfoTXT == null)
        {
            return;
        }

        bool hasContent = !string.IsNullOrWhiteSpace(infoText);
        InfoTXT.gameObject.SetActive(hasContent);
        InfoTXT.text = hasContent ? infoText : string.Empty;
    }

    // Legacy path removed

    private void SpawnAmount(int amount)
    {
        if (amountTemplate == null || contentRoot == null)
        {
            return;
        }

        TMP_Text instance = Instantiate(amountTemplate, contentRoot);
        instance.gameObject.SetActive(true);
        instance.text = amount.ToString();

        spawnedElements.Add(instance.gameObject);
    }

    // Legacy icon lookup removed; icons come from ResourceTypeDef

    private void SpawnIcon(Sprite icon)
    {
        if (iconTemplate == null || contentRoot == null)
        {
            return;
        }

        Image instance = Instantiate(iconTemplate, contentRoot);
        instance.gameObject.SetActive(true);
        instance.sprite = icon;
        instance.enabled = icon != null;
        spawnedElements.Add(instance.gameObject);
    }

    private void EnsureTemplatesInactive()
    {
        if (iconTemplate != null)
        {
            iconTemplate.gameObject.SetActive(false);
        }

        if (amountTemplate != null)
        {
            amountTemplate.gameObject.SetActive(false);
        }
    }
}
}






