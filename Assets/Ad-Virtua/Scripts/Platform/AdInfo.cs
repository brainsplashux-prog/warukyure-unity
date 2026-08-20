using UnityEngine.Scripting;

namespace Ad_Virtua.Runtime
{
    /// <summary>
    /// 広告ID情報
    /// IL2CPPストリッピング対策として[Preserve]属性を付与
    /// </summary>
    public class AdInfo
    {
        [Preserve] public string advertisingId;
        [Preserve] public bool trackingEnabled;
    }
}
