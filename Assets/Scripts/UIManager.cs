using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Animator gear;
    [SerializeField] private Animator paper;


    private void Start()
    {
         
    }

    public void OpenGear()
    {
        if(gear.GetBool("Opened") == false)
        {
            gear.SetBool("Opened", true);
            paper.SetBool("Opened", true);
        }
        else
        {
            gear.SetBool("Opened", false);
            paper.SetBool("Opened", false);
        }
    }

}
