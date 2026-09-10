using UnityEngine;

public class PoiErrManager : MonoBehaviour
{
    public void OnRetry()
    {
        PoiErr.OnRetry?.Invoke();
    }

    public void OnBack()
    {
        PoiErr.OnBack?.Invoke();
    }
}
