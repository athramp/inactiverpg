using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class IdlePanel : MonoBehaviour {
  public TMP_Text previewText;
  public Button claimBtn;

  void Awake() {
    if (claimBtn != null) {
      claimBtn.onClick.RemoveAllListeners();
      claimBtn.onClick.AddListener(OnClaim);
    }
  }
  async void OnEnable() {
    while (Game.I == null || Game.I.Backend == null || !Game.I.Backend.IsReady)
      await System.Threading.Tasks.Task.Yield();

    var res = await Game.I.Backend.Call("previewOfflineKills");
    var dict = FirebaseResult.ToDict(res.Data);
    var kills = FirebaseResult.GetLong(dict, "kills");
    var gold  = FirebaseResult.GetLong(dict, "gold");
    previewText.text = $"Kills: {kills}  |  Gold: {gold}";
  }

  public void OnClaim() { StartCoroutine(ClaimRoutine()); }

  private IEnumerator ClaimRoutine() {
    if (claimBtn) claimBtn.interactable = false;
    var task = Game.I.Backend.Call("claimOfflineLoot");
    yield return new WaitUntil(() => task.IsCompleted);

    if (task.IsFaulted || task.Result == null) {
      Debug.LogError(task.Exception?.ToString() ?? "claimOfflineLoot null");
      if (previewText) previewText.text = "Error claiming.";
      if (claimBtn) claimBtn.interactable = true;
      yield break;
    }

    var dict = FirebaseResult.ToDict(task.Result.Data);
    var kills = FirebaseResult.GetLong(dict, "kills");
    var gold  = FirebaseResult.GetLong(dict, "gold");
    if (previewText) previewText.text = $"Claimed → Kills: {kills}, Gold: {gold}";
    if (claimBtn) claimBtn.interactable = true;
  }
}
