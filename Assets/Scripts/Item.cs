using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    public enum ObjectType {Weapon, Resource}

    public Sprite objectImage;
    public ObjectType objectType;
    public float value;
    public int maxStack;

}
