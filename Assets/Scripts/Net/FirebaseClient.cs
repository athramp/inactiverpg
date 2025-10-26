// FirebaseClient.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Functions;
using Firebase;
using UnityEngine;

namespace Net {
  public class FirebaseClient {
    public FirebaseAuth Auth { get; private set; }
    public FirebaseFunctions Functions { get; private set; }
    public bool IsReady { get; private set; }

    public bool AllowAnonymousSignIn = false; // set true only if you need anon calls before login

    public async Task InitializeAsync() {
      await FirebaseGate.WaitUntilReady();

      var app = FirebaseApp.DefaultInstance; // ensure created by gate
      Auth = FirebaseAuth.DefaultInstance;
      Functions = FirebaseFunctions.DefaultInstance;

      if (AllowAnonymousSignIn && Auth.CurrentUser == null) {
        await Auth.SignInAnonymouslyAsync();
      }

      var opts = app?.Options;
      Debug.Log($"Firebase project: {opts?.ProjectId}, appId: {opts?.AppId}");
      IsReady = (Auth != null && Functions != null);
    }

    public async Task<HttpsCallableResult> Call(string name, Dictionary<string, object> data = null) {
      while (!IsReady) await Task.Yield();
      return await Functions.GetHttpsCallable(name).CallAsync(data ?? new Dictionary<string, object>());
    }
  }
}
