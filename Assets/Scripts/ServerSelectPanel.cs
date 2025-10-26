using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable] public class ServerInfo {
  public string id;
  public string displayName;
  public string region;
}

public class ServerSelectPanel : MonoBehaviour {
  public TMP_Dropdown serverDropdown;
  public Button btnPlay;
  public UIRootController uiRoot;

  List<ServerInfo> servers = new() {
    new ServerInfo{ id="windless-18", displayName="Windless Desert 18", region="EU"},
    new ServerInfo{ id="limbo-01", displayName="Limbo 01", region="NA"},
  };
  int selected;

  void Start(){
    if (serverDropdown == null || btnPlay == null || uiRoot == null) {
      Debug.LogError("ServerSelectPanel: missing refs.");
      return;
    }
    serverDropdown.options.Clear();
    foreach (var s in servers)
      serverDropdown.options.Add(new TMP_Dropdown.OptionData(s.displayName));
    serverDropdown.value = 0; serverDropdown.RefreshShownValue();
    serverDropdown.onValueChanged.AddListener(i => selected = i);

    btnPlay.onClick.AddListener(OnPlay);
  }

  async void OnPlay(){
    PlayerPrefs.SetString("serverId", servers[selected].id);
    PlayerPrefs.Save();

    // TODO: optional handshake with Firebase here

    uiRoot.ShowGameHUD();
  }
}
