using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Inventario : MonoBehaviour
{

    public List<Item> objetos;
    public RectTransform content;
    public GameObject itemPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        content.sizeDelta = new Vector2(0, 50);
        for (int i = 0; i < objetos.Count; i++)
        {
            GameObject uiItemClone = Instantiate(itemPrefab, content);
            uiItemClone.transform.GetChild(0).GetComponent<Image>().sprite = objetos[i].objectImage;
            uiItemClone.transform.GetChild(1).GetComponent<Text>().text = objetos[i].nombre;
            switch (objetos[i].objectType)
            {
                case Item.ObjectType.Weapon:
                    uiItemClone.transform.GetChild(2).GetComponent<Text>().text = "Velocidad (Seg): ";
                    break;

                case Item.ObjectType.Armor:
                    uiItemClone.transform.GetChild(2).GetComponent<Text>().text = "Armadura: ";
                    break;

                case Item.ObjectType.Consumable:
                    uiItemClone.transform.GetChild(2).GetComponent<Text>().text = "Vida: ";
                    break;
            }
            uiItemClone.transform.GetChild(2).GetComponent<Text>().text += objetos[i].value.ToString();
            content.sizeDelta += new Vector2(0, 200);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
