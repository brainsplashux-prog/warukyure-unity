using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ad_Virtua.Conversion
{
    /// <summary>
    /// バナー画像の制御クラス
    /// 画像の設定とタップ時のURL遷移を担当
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BannerImageController : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>
        /// タップ時に遷移するURL
        /// </summary>
        public string TargetUrl;

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        /// <summary>
        /// バナー画像をセットする
        /// </summary>
        /// <param name="sprite">設定するSprite</param>
        public void SetBannerImage(Sprite sprite)
        {
            if (_image != null)
            {
                _image.sprite = sprite;
            }
        }

        /// <summary>
        /// バナータップ時の処理
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(TargetUrl))
            {
                Application.OpenURL(TargetUrl);
            }
        }
    }
}
