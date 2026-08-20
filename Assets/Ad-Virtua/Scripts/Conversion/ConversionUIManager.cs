using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ad_Virtua.Conversion
{
    /// <summary>
    /// コンバージョンUI管理クラス
    /// BannerImageControllerへのアクセスと閉じるボタンの制御を担当
    /// </summary>
    public class ConversionUIManager : MonoBehaviour
    {
        /// <summary>
        /// バナー画像コントローラーへの参照
        /// </summary>
        [SerializeField]
        private BannerImageController _bannerImageController;

        /// <summary>
        /// 閉じるボタンへの参照
        /// </summary>
        [SerializeField]
        private Button _closeButton;

        /// <summary>
        /// UI閉じた時のコールバック用イベント
        /// </summary>
        public event Action OnConversionClosed;

        /// <summary>
        /// バナー画像コントローラーを取得
        /// </summary>
        public BannerImageController BannerImageController => _bannerImageController;

        private void Awake()
        {
            // 閉じるボタンのクリックイベントを登録
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        private void OnDestroy()
        {
            // イベントリスナーを解除
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }
        }

        /// <summary>
        /// バナーを初期化する
        /// </summary>
        /// <param name="sprite">バナー画像</param>
        /// <param name="url">遷移先URL</param>
        public void SetupBanner(Sprite sprite, string url)
        {
            if (_bannerImageController != null)
            {
                _bannerImageController.SetBannerImage(sprite);
                _bannerImageController.TargetUrl = url;
            }
        }

        /// <summary>
        /// 閉じるボタン押下時の処理
        /// 親のCanvasを破棄する
        /// </summary>
        public void OnCloseButtonClicked()
        {
            // イベントを発火
            OnConversionClosed?.Invoke();

            // 親のCanvasを取得して破棄
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
            else
            {
                // Canvasが見つからない場合は自分自身を破棄
                Destroy(gameObject);
            }
        }
    }
}
