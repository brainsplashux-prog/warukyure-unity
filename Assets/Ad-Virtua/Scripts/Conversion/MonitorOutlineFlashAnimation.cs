using UnityEngine;

namespace Ad_Virtua.Conversion
{
    /// <summary>
    /// マテリアルのAlpha値をフラッシュアニメーションさせるコンポーネント
    /// 透明度を0→1→0でループさせる
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class MonitorOutlineFlashAnimation : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("アニメーション対象のRenderer（未設定の場合は自身のRendererを使用）")]
        private Renderer targetRenderer;

        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("アニメーション速度（1.0で約2秒/サイクル）")]
        private float animationSpeed = 1.0f;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int AlphaPropertyId = Shader.PropertyToID("_Alpha");

        private void Start()
        {
            // Rendererが未設定の場合は自身から取得
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            // PropertyBlockを初期化
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (targetRenderer == null || propertyBlock == null)
                return;

            // PingPongで0→1→0をループ
            float alpha = Mathf.PingPong(Time.time * animationSpeed, 1f);

            // PropertyBlockでAlpha値を設定
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(AlphaPropertyId, alpha);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDisable()
        {
            // 無効化時にAlphaを1.0に戻す
            if (targetRenderer != null && propertyBlock != null)
            {
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(AlphaPropertyId, 1f);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
