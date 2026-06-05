using UnityEngine;

[CreateAssetMenu(fileName = "Recipe", menuName = "Scriptable Objects/Recipe")]
public class Recipe : ScriptableObject
{
    public Ingredient[] ingredients;
    public Item result;
    public int resultQuantity;


    [System.Serializable]
    public class Ingredient
    {
        public Item item;
        public int quantity;
    }

}
