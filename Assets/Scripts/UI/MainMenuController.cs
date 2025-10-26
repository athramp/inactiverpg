// MainMenuController.cs
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public GameObject LoginPanel;

    public async void OnPlayClicked()
    {
        await FirebaseGate.WaitUntilReady();

        var user = EmailAuth.I?.CachedUser();
        if (user != null) {
            var server = PlayerPrefs.GetString("serverId", "WindlessDesert18");
            var doc = await CharacterService.GetAsync(server, user.UserId);
            if (doc != null) {
                await CharacterService.TouchLoginAsync(server, user.UserId);
                // go straight to game scene here if desired
                return;
            }
        }
        if (LoginPanel) LoginPanel.SetActive(true);
    }
}
