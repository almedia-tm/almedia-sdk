using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using AlmediaLinkPopupController = AlmediaSDK.AlmediaLinkPopupController;
using AlmediaLinkSource = AlmediaSDK.AlmediaLinkSource;

namespace AlmediaLink.UI
{
    [DisallowMultipleComponent]
    public class LinkPopupController : AlmediaLinkPopupController
    {
        [Header("Animator")]
        [SerializeField] private PopupAnimator _animator;

        [Header("Buttons")]
        [SerializeField] private Button _ctaButton;
        [SerializeField] private Button _closeButton;

        [Header("Text Fields")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _ctaButtonLabel;

        [Header("Benefit Box")]
        [SerializeField] private TMP_Text _benefit1TitleText;
        [FormerlySerializedAs("_bodyText")]
        [SerializeField] private TMP_Text _benefit1DescriptionText;
        [SerializeField] private TMP_Text _benefit2TitleText;
        [FormerlySerializedAs("_bonusText")]
        [SerializeField] private TMP_Text _benefit2DescriptionText;

        [Header("Colors")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _ctaButtonImage;

        /// <summary>
        /// When false, this controller belongs to a host-supplied override Prefab Variant,
        /// so the SDK must not overlay settings onto it. Set by AlmediaLinkUIManager before
        /// the UI activates; true for the SDK's built-in prefabs.
        /// </summary>
        [Tooltip("Apply text and color values from AlmediaLinkSettings to this instance. Untick on a Prefab Variant you have fully authored yourself.")]
        [SerializeField] internal bool ApplyHostSettings = true;

        private void Awake()
        {
            if (!Application.isPlaying) return;

            if (_ctaButton != null)
                _ctaButton.onClick.AddListener(StartLinking);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Dismiss);

            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            ApplySettings();
            EventSystemCheck.WarnIfMissing(this, "LinkPopup");
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying) return;

            if (_ctaButton != null)
                _ctaButton.onClick.RemoveListener(StartLinking);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Dismiss);
        }

        /// <summary>
        /// Opens this instance with <see cref="AlmediaLinkSource.ShowLink"/> as the source. Prefer
        /// <see cref="AlmediaSDK.Almedia.ShowLink()"/>, which checks the player's status first.
        /// </summary>
        public void Show() => Show(null);

        /// <summary>
        /// Opens this instance and reports <paramref name="source"/> through the base class events. A null
        /// source reads as <see cref="AlmediaLinkSource.ShowLink"/>. Prefer
        /// <see cref="AlmediaSDK.Almedia.ShowLink(AlmediaLinkSource)"/>, which checks the player's status first.
        /// </summary>
        public void Show(AlmediaLinkSource source) => AlmediaLinkUIManager.ShowHostInstance(this, source);

        protected override void ShowPopup()
        {
            ApplySettings();
            _animator.Show();
        }

        protected override void HidePopup()
        {
            _animator.Hide();
        }

        private void ApplySettings()
        {
            var settings = AlmediaLinkSettings.Load();
            if (settings == null) return;

            if (!Application.isPlaying || !ApplyHostSettings) return;

            // Text
            if (_titleText != null)
                _titleText.text = settings.PopupTitle;
            if (_ctaButtonLabel != null)
                _ctaButtonLabel.text = settings.CtaButtonText;

            if (_benefit1TitleText != null)
                _benefit1TitleText.text = settings.Benefit1Title;
            if (_benefit1DescriptionText != null)
                _benefit1DescriptionText.text = settings.Benefit1Description;
            if (_benefit2TitleText != null)
                _benefit2TitleText.text = settings.Benefit2Title;
            if (_benefit2DescriptionText != null)
                _benefit2DescriptionText.text = settings.Benefit2Description;

            // Colors
            if (_ctaButtonLabel != null)
                _ctaButtonLabel.color = settings.CtaButtonTextColor;
            if (_backgroundImage != null)
                _backgroundImage.color = settings.PopupBackgroundColor;
            if (_ctaButtonImage != null)
                _ctaButtonImage.color = settings.CtaButtonColor;
        }
    }
}
