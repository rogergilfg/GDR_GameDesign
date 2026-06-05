using UnityEngine;
using UnityEngine.UI;

public class CraftingManager : MonoBehaviour
{
    private GameObject craftingMenu;
    [SerializeField] private Recipe[] recipes;
    [SerializeField] private Inventario inventario;
    [SerializeField] private Animator craftingMenuAnimator;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            craftingMenuAnimator.SetBool("Opened", !craftingMenuAnimator.GetBool("Opened"));
        }
    }

    public void Craft(Recipe recipe)
    {
        if (inventario.HasIngredients(recipe.ingredients))
        {
            inventario.RemoveIngredients(recipe.ingredients);
            inventario.AddItem(recipe.result, recipe.resultQuantity);
        }
        else
        {
            Debug.Log("No tienes los ingredientes necesarios para esta receta.");
        }
    }
}
