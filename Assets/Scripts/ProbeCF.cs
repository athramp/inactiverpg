using UnityEngine;

public class ProbeCF : MonoBehaviour {
  async void Start() {
    while (Game.I == null || Game.I.Backend == null || !Game.I.Backend.IsReady)
      await System.Threading.Tasks.Task.Yield();

    var r1 = await Game.I.Backend.Call("getServerTime");
    var d1 = FirebaseResult.ToDict(r1.Data);
    Debug.Log("CF OK epoch: " + FirebaseResult.GetLong(d1, "epochSeconds"));

    var r2 = await Game.I.Backend.Call("hello");
    var d2 = FirebaseResult.ToDict(r2.Data);
    Debug.Log($"hello ok={FirebaseResult.GetBool(d2,"ok")} num={FirebaseResult.GetLong(d2,"num")} msg={FirebaseResult.GetString(d2,"msg")}");
  }
}
