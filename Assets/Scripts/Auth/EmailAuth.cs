// EmailAuth.cs
using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class EmailAuth : MonoBehaviour
{
    public static EmailAuth I { get; private set; }
    private FirebaseAuth _auth;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // If gate already flipped, we can grab DefaultInstance immediately.
        if (FirebaseGate.IsReady)
            _auth = FirebaseAuth.DefaultInstance;
    }

    private async Task<FirebaseAuth> GetAuthAsync()
    {
        await FirebaseGate.WaitUntilReady();
        if (_auth == null) _auth = FirebaseAuth.DefaultInstance;
        if (_auth == null) throw new NullReferenceException("FirebaseAuth.DefaultInstance is null.");
        return _auth;
    }

    public async Task<FirebaseUser> SignInAsync(string email, string password)
    {
        var auth = await GetAuthAsync();
        var cred = await auth.SignInWithEmailAndPasswordAsync(email, password);
        if (cred?.User == null) throw new Exception("Sign-in returned no user.");
        return cred.User;
    }

    public async Task<FirebaseUser> SignUpAsync(string email, string password)
    {
        var auth = await GetAuthAsync();
        var cred = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
        if (cred?.User == null) throw new Exception("Sign-up returned no user.");
        return cred.User;
    }

    public async Task SendPasswordResetAsync(string email)
    {
        var auth = await GetAuthAsync();
        await auth.SendPasswordResetEmailAsync(email);
    }

    public FirebaseUser CachedUser()
    {
        // Safe even if gate not ready; will be null until ready+login.
        return _auth != null ? _auth.CurrentUser : FirebaseAuth.DefaultInstance?.CurrentUser;
    }
}
