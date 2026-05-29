using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    public enum ObjectType {Weapon, Armor, Consumable, Resource}

    public Sprite objectImage;
    public string nombre;
    public ObjectType objectType;
    public float value;
    public int maxStack;
}
