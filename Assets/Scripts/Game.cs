using UnityEngine;
using System.Threading.Tasks;
using Net;

public class Game : MonoBehaviour {
    public static Game I { get; private set; }
    public FirebaseClient Backend { get; private set; }

    async void Awake() {
        if (I != null) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

        Backend = new FirebaseClient();
        await Backend.InitializeAsync();

        Debug.Log("Game initialized.");
    }
}
