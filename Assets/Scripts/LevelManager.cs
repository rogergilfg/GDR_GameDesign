using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    [Header("Resources")]
    private int Wood;
    [SerializeField] private Sprite woodImage;

    private int Stone;
    [SerializeField] private Sprite stoneImage;

    private int Niquel;
    [SerializeField] private Sprite niquelImage;

    private int Coal;
    [SerializeField] private Sprite coalImage;

    private int Cooper;
    [SerializeField] private Sprite cooperImage;

    [Header("UI")]

    [SerializeField] private RectTransform[] slots;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
