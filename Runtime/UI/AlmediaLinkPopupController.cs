using System;
using UnityEngine;

namespace AlmediaSDK
{
    /// <summary>
    /// The component that shows the link popup. <see cref="Almedia.ShowLink()"/> and the LinkButton prefab
    /// instantiate the assigned prefab once, as a root object kept across scenes, and open it on every
    /// call. Derive from it for your own popup: implement <see cref="ShowPopup"/> and <see cref="HidePopup"/>
    /// with your visuals, and wire your buttons to <see cref="StartLinking"/> and <see cref="Dismiss"/>.
    /// The static events report every popup built on this class, with the <see cref="AlmediaLinkSource"/>
    /// that opened it.
    /// </summary>
    public abstract class AlmediaLinkPopupController : MonoBehaviour
    {
        /// <summary>
        /// A popup opened. Fires once per open, after <see cref="ShowPopup"/> returned. The return after
        /// the player cancels the linking screen does not fire it.
        /// </summary>
        public static event Action<AlmediaLinkSource> OnShown;

        /// <summary>
        /// The player tapped the popup's button. Linking starts right after. Can fire more than once
        /// per open.
        /// </summary>
        public static event Action<AlmediaLinkSource> OnCtaClicked;

        /// <summary>The player closed the popup without linking.</summary>
        public static event Action<AlmediaLinkSource> OnDismissed;

        private bool _isOpen;
        private bool _behindLinkingScreen;
        private bool _showing;

        /// <summary>
        /// What opened the popup. Set before <see cref="ShowPopup"/> runs and cleared after the
        /// <see cref="HidePopup"/> of the close returns. Null while the popup is closed.
        /// </summary>
        protected AlmediaLinkSource Source { get; private set; }

        /// <summary>
        /// Shows the visuals. Called when the popup opens, and again when the player closes the linking
        /// screen without linking.
        /// </summary>
        protected abstract void ShowPopup();

        /// <summary>
        /// Hides the visuals. Called when the popup closes, and when the linking screen covers it.
        /// </summary>
        protected abstract void HidePopup();

        /// <summary>
        /// Call when the player taps the popup's button. Records the tap, raises <see cref="OnCtaClicked"/>
        /// and starts linking. The popup hides while the linking screen is open and comes back if the
        /// player closes it without linking. Does nothing while the popup is not open, while it waits
        /// behind the linking screen, and inside <see cref="ShowPopup"/>.
        /// </summary>
        protected void StartLinking()
        {
            if (!TakesTaps) return;
            Almedia.TrackPopupCtaClick();
            EventDispatch.Raise(nameof(OnCtaClicked), OnCtaClicked, Source);
            Almedia.StartLinking(AlmediaPlacementType.Popup);
        }

        /// <summary>
        /// Call when the player closes the popup. Records the dismiss, raises <see cref="OnDismissed"/>
        /// and hides the popup. Does nothing while the popup is not open, while it waits behind the
        /// linking screen, and inside <see cref="ShowPopup"/>.
        /// </summary>
        protected void Dismiss()
        {
            if (!TakesTaps) return;
            Almedia.TrackPopupDismiss();
            EventDispatch.Raise(nameof(OnDismissed), OnDismissed, Source);
            Close();
        }

        internal bool IsOpen => _isOpen && (_behindLinkingScreen || gameObject.activeInHierarchy);

        private bool TakesTaps => _isOpen && !_behindLinkingScreen && !_showing;

        internal void Present(AlmediaLinkSource source)
        {
            Source = source ?? AlmediaLinkSource.ShowLink;
            _isOpen = true;
            _behindLinkingScreen = false;
            try
            {
                ShowVisuals();
            }
            catch
            {
                _isOpen = false;
                Source = null;
                throw;
            }
            Almedia.TrackPopupShow();
            EventDispatch.Raise(nameof(OnShown), OnShown, Source);
        }

        internal void Close()
        {
            if (!_isOpen) return;
            bool visible = !_behindLinkingScreen;
            _isOpen = false;
            _behindLinkingScreen = false;
            if (visible) HideVisuals();
            Source = null;
        }

        internal void HandleStatusChanged(AlmediaStatus status)
        {
            if (status is AlmediaStatus.NotInitialized || status is AlmediaStatus.Eligible) return;
            Close();
        }

        internal void HandleScreenPresented(AlmediaScreen screen)
        {
            if (!_isOpen || _behindLinkingScreen) return;
            if (screen != AlmediaScreen.Linking)
            {
                Close();
                return;
            }

            _behindLinkingScreen = true;
            HideVisuals();
        }

        internal void HandleScreenDismissed(AlmediaScreen screen, AlmediaInAppScreenResult result)
        {
            if (!_isOpen || !_behindLinkingScreen || screen != AlmediaScreen.Linking) return;
            if (!(result is AlmediaInAppScreenResult.Cancelled) || !(Almedia.Status is AlmediaStatus.Eligible))
            {
                Close();
                return;
            }

            _behindLinkingScreen = false;
            try
            {
                ShowVisuals();
            }
            catch (Exception e)
            {
                Report("ShowPopup", e);
                Close();
            }
        }

        private void ShowVisuals()
        {
            _showing = true;
            try
            {
                ShowPopup();
            }
            finally
            {
                _showing = false;
            }
        }

        private void HideVisuals()
        {
            try
            {
                HidePopup();
            }
            catch (Exception e)
            {
                Report("HidePopup", e);
            }
        }

        private void Report(string method, Exception e)
            => AlmediaLog.Error($"{GetType().Name}.{method} failed: {e.GetType().Name}: {e.Message}");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnDomainReload()
        {
            OnShown = null;
            OnCtaClicked = null;
            OnDismissed = null;
        }
    }
}
