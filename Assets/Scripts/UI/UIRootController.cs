using UnityEngine;

public class UIRootController : MonoBehaviour {
  public GameObject MainMenu;
  public GameObject GameHUD;

  void Awake(){ ShowMainMenu(); }

  public void ShowMainMenu(){
    if (MainMenu) MainMenu.SetActive(true);
    if (GameHUD) GameHUD.SetActive(false);
  }
  public void ShowGameHUD(){
    if (MainMenu) MainMenu.SetActive(false);
    if (GameHUD) GameHUD.SetActive(true);
  }
}
