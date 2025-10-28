using UnityEngine;

public class FollowWorldTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 1.5f, 0); // tweak up above head


    Camera cam; RectTransform rt;
    void Awake(){ cam = Camera.main; rt = (RectTransform)transform; }
    void LateUpdate(){
        if (!target) { gameObject.SetActive(false); return; }
        rt.position = cam.WorldToScreenPoint(target.position + offset);
    }
}
