// LoginPanel.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;
using Firebase;

public class LoginPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_InputField EmailInput;
    public TMP_InputField PasswordInput;
    public TMP_Text ErrorText;
    public Button SignInButton;
    public Button SignUpButton;
    public Button ForgotButton;

    [Header("Next UI")]
    public GameObject CharacterCreatePanel;

    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");

    void OnEnable()
    {
        SetError("");
        if (EmailInput) EmailInput.text = PlayerPrefs.GetString("lastEmail", "");
    }

    public void OnSignInClicked() { _ = RunAuthFlow(async () =>
    {
        var (email, pass) = ReadInputs();
        var user = await EmailAuth.I.SignInAsync(email, pass);
        PlayerPrefs.SetString("lastEmail", email);

        var doc = await CharacterService.GetAsync(ServerId, user.UserId);
        if (doc == null)
        {
            gameObject.SetActive(false);
            Must(CharacterCreatePanel, "CharacterCreatePanel").SetActive(true);
        }
        else
        {
            await CharacterService.TouchLoginAsync(ServerId, user.UserId);
            var boot = FindObjectOfType<RuntimeBootstrap>();
            if (boot != null) await boot.StartForCurrentUserAsync();
            gameObject.SetActive(false);
        }
    }); }

    public void OnSignUpClicked() { _ = RunAuthFlow(async () =>
    {
        var (email, pass) = ReadInputs();
        if (pass.Length < 6) throw new System.Exception("Password must be at least 6 characters.");
        var user = await EmailAuth.I.SignUpAsync(email, pass);
        PlayerPrefs.SetString("lastEmail", email);

        gameObject.SetActive(false);
        Must(CharacterCreatePanel, "CharacterCreatePanel").SetActive(true);
    }); }

    public void OnForgotClicked() { _ = RunAuthFlow(async () =>
    {
        var email = Must(EmailInput, "EmailInput").text.Trim();
        if (string.IsNullOrEmpty(email)) throw new System.Exception("Enter your email first.");
        await EmailAuth.I.SendPasswordResetAsync(email);
        SetError("Reset email sent.");
    }); }

    // ---- Helpers ----
    async Task RunAuthFlow(System.Func<Task> body)
    {
        try
        {
            SetInteractable(false);
            SetError("Initializing Firebase…");
            await FirebaseGate.WaitUntilReady();
            if (FirebaseGate.InitException != null)
                throw FirebaseGate.InitException;

            if (EmailAuth.I == null)
                throw new System.Exception("EmailAuth not found in scene. Place the EmailAuth prefab in your first scene.");

            SetError(""); // clear
            await body();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            SetError(HumanizeAuthError(e));
        }
        finally
        {
            SetInteractable(true);
        }
    }

    (string email, string pass) ReadInputs()
    {
        var email = Must(EmailInput, "EmailInput").text?.Trim();
        var pass  = Must(PasswordInput, "PasswordInput").text;
        if (string.IsNullOrEmpty(email)) throw new System.Exception("Please enter your email.");
        if (string.IsNullOrEmpty(pass))  throw new System.Exception("Please enter your password.");
        return (email, pass);
    }

    T Must<T>(T obj, string name) where T : class
    {
        if (obj == null) throw new System.NullReferenceException($"{name} is not assigned on LoginPanel.");
        return obj;
    }

    void SetError(string msg) { if (ErrorText) ErrorText.text = msg ?? ""; }
    void SetInteractable(bool v)
    {
        if (SignInButton) SignInButton.interactable = v;
        if (SignUpButton) SignUpButton.interactable = v;
        if (ForgotButton) ForgotButton.interactable = v;
    }

    string HumanizeAuthError(System.Exception ex)
    {
        while (ex is System.AggregateException agg && agg.InnerException != null)
            ex = agg.InnerException;

        if (ex is FirebaseException fe)
        {
            var code = (Firebase.Auth.AuthError)fe.ErrorCode;
            switch (code)
            {
                case Firebase.Auth.AuthError.InvalidEmail:         return "That email address is not valid.";
                case Firebase.Auth.AuthError.WrongPassword:        return "Incorrect password.";
                case Firebase.Auth.AuthError.UserNotFound:         return "No account found with that email.";
                case Firebase.Auth.AuthError.EmailAlreadyInUse:    return "That email is already in use.";
                case Firebase.Auth.AuthError.WeakPassword:         return "Password is too weak (min 6 characters).";
                case Firebase.Auth.AuthError.NetworkRequestFailed: return "Network error. Check your connection.";
                default: return fe.Message ?? "Auth failed.";
            }
        }
        return ex.Message ?? "Something went wrong.";
    }
}
