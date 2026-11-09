using System;
using System.Collections.Generic;
using AlmediaLink.Models;
using UnityEngine;
using UnityEngine.UI;
using AlmediaLinkPopupController = AlmediaSDK.AlmediaLinkPopupController;
using AlmediaLinkSource = AlmediaSDK.AlmediaLinkSource;
using NewApi = AlmediaSDK.Almedia;
using Object = UnityEngine.Object;

namespace AlmediaLink.UI
{
    internal static class AlmediaLinkUIManager
    {
        private const int MaxDisplayedNotifications = 3;

        private static NotificationCardController _notificationCard;
        private static ActivityOverlayController _activityOverlay;
        private static List<AlmediaNotification> _lastNotifications;
        private static bool _initialized;
        private static readonly HashSet<string> _missingPrefabsLogged = new HashSet<string>();
        private static AlmediaLinkPopupController _popup;
        private static AlmediaLinkPopupController _popupPrefab;
        private static bool _missingLinkPopupWarned;
        private static bool _popupWithoutCanvasWarned;

        internal static void Initialize()
        {
            if (_initialized) return;
            AlmediaLinkSDK.OnNotificationsReceived += HandleNotifications;
            NotificationCardController.OnStackedCardTapped += HandleStackedCardTapped;
            NewApi.OnStatusChanged += HandleStatusChanged;
            NewApi.OnLinkCompleted += HandleLinkCompleted;
            NewApi.OnErrorOccurred += HandleErrorOccurred;
            NewApi.OnScreenPresented += HandleScreenPresented;
            NewApi.OnScreenDismissed += HandleScreenDismissed;
            _initialized = true;
        }

        #region Link popup

        internal static void ShowLinkPopup(AlmediaLinkPopupController prefab, AlmediaLinkSource source)
        {
            if (prefab == null) return; // the caller owns the fallback and the warning
            Initialize();
            if (PopupAlreadyOpen()) return;

            if (_popup != null && _popupPrefab != prefab)
                DropPopup();
            if (_popup == null)
            {
                WarnIfNoCanvas(prefab);
                _popup = Object.Instantiate(prefab);
                Object.DontDestroyOnLoad(_popup.gameObject);
                _popupPrefab = prefab;
            }

            try
            {
                _popup.Present(source);
            }
            catch (Exception e)
            {
                var failed = _popup;
                _popup = null;
                _popupPrefab = null;
                if (failed != null)
                {
                    failed.gameObject.SetActive(false);
                    Object.Destroy(failed.gameObject);
                }
                AlmediaLog.Error($"Link popup '{prefab.name}' failed to open: {e.GetType().Name}: {e.Message}");
            }
        }

        internal static void ShowConfiguredLinkPopup(AlmediaLinkSource source)
        {
            if (PopupAlreadyOpen()) return;

            var settings = AlmediaLinkSettings.Load();
            AlmediaLinkPopupController popup = null;
            if (settings != null)
                popup = settings.LinkPopupPrefab != null ? settings.LinkPopupPrefab : settings.LegacyLinkPopupOverride;

            if (popup != null)
            {
                ShowLinkPopup(popup, source);
                return;
            }

            if (!_missingLinkPopupWarned)
            {
                _missingLinkPopupWarned = true;
                AlmediaLog.Warning(
                    "ShowLink: no Link Popup assigned in Almedia > Settings; linking starts directly. " +
                    "Assign the Link Popup there to show the popup first.");
            }
            AlmediaLinkSDK.StartLinking(PlacementType.Popup);
        }

        // An instance the host created itself and opens through LinkPopupController.Show().
        internal static void ShowHostInstance(AlmediaLinkPopupController popup, AlmediaLinkSource source)
        {
            try
            {
                Initialize();
                if (popup.IsOpen)
                {
                    AlmediaLog.Debug("A link popup is already open.");
                    return;
                }
                if (_popup != popup)
                {
                    LivePopup()?.Close();
                    DropPopup();
                    _popup = popup;
                    _popupPrefab = null;
                }
                popup.Present(source);
            }
            catch (Exception e)
            {
                if (_popup == popup) _popup = null;
                AlmediaLog.Error($"Link popup '{popup.name}' failed to open: {e.GetType().Name}: {e.Message}");
            }
        }

        // _popupPrefab is null for a host-owned instance, which is released but never destroyed here.
        private static void DropPopup()
        {
            if (_popup != null && _popupPrefab != null)
                Object.Destroy(_popup.gameObject);
            _popup = null;
            _popupPrefab = null;
        }

        private static void WarnIfNoCanvas(AlmediaLinkPopupController prefab)
        {
            if (_popupWithoutCanvasWarned || prefab.GetComponent<Canvas>() != null) return;
            _popupWithoutCanvasWarned = true;
            AlmediaLog.Warning($"Link popup '{prefab.name}' has no Canvas on its root object, so it cannot render. Put a Canvas on the prefab's root.");
        }

        private static bool PopupAlreadyOpen()
        {
            var popup = LivePopup();
            if (popup == null || !popup.IsOpen) return false;
            AlmediaLog.Debug("A link popup is already open.");
            return true;
        }

        // Null once the host has destroyed the instance.
        private static AlmediaLinkPopupController LivePopup()
        {
            if (_popup == null)
            {
                _popup = null;
                _popupPrefab = null;
            }
            return _popup;
        }

        private static void HandleStatusChanged(AlmediaSDK.AlmediaStatus status) => LivePopup()?.HandleStatusChanged(status);

        private static void HandleLinkCompleted(string linkedAt) => LivePopup()?.Close();

        private static void HandleErrorOccurred(AlmediaSDK.AlmediaError error)
        {
            if (error == null || error.Code != AlmediaSDK.AlmediaErrorCode.LinkingFailed) return;
            LivePopup()?.Close();
        }

        private static void HandleScreenPresented(AlmediaSDK.AlmediaScreen screen) => LivePopup()?.HandleScreenPresented(screen);

        private static void HandleScreenDismissed(AlmediaSDK.AlmediaScreen screen, AlmediaSDK.AlmediaInAppScreenResult result)
            => LivePopup()?.HandleScreenDismissed(screen, result);

        #endregion

        internal static void Cleanup()
        {
            AlmediaLinkSDK.OnNotificationsReceived -= HandleNotifications;
            NotificationCardController.OnStackedCardTapped -= HandleStackedCardTapped;
            NewApi.OnStatusChanged -= HandleStatusChanged;
            NewApi.OnLinkCompleted -= HandleLinkCompleted;
            NewApi.OnErrorOccurred -= HandleErrorOccurred;
            NewApi.OnScreenPresented -= HandleScreenPresented;
            NewApi.OnScreenDismissed -= HandleScreenDismissed;
            _initialized = false;
            _lastNotifications = null;
            _missingPrefabsLogged.Clear();
            _missingLinkPopupWarned = false;
            _popupWithoutCanvasWarned = false;

            if (_notificationCard != null)
            {
                var canvas = _notificationCard.GetComponentInParent<Canvas>();
                if (canvas != null)
                    Object.Destroy(canvas.gameObject);
            }
            _notificationCard = null;
            _activityOverlay = null; // destroyed with canvas (child)

            DropPopup();
        }

        private static void HandleNotifications(List<AlmediaNotification> notifications)
        {
            if (!ShouldShowDefaultNotificationUI()) return;
            _lastNotifications = CapToLatest(notifications);
            EnsureNotificationCardExists();
            if (_notificationCard != null)
                _notificationCard.ShowNotifications(_lastNotifications);
        }

        private static List<AlmediaNotification> CapToLatest(List<AlmediaNotification> notifications)
        {
            if (notifications == null || notifications.Count <= MaxDisplayedNotifications)
                return notifications;
            return notifications.GetRange(notifications.Count - MaxDisplayedNotifications, MaxDisplayedNotifications);
        }

        private static void HandleStackedCardTapped()
        {
            if (_lastNotifications == null || _lastNotifications.Count == 0) return;
            EnsureActivityOverlayExists();
            if (_activityOverlay != null)
                _activityOverlay.Show(_lastNotifications);
        }

        private static bool ShouldShowDefaultNotificationUI()
        {
            var settings = AlmediaLinkSettings.Load();
            return settings != null && settings.EnableDefaultNotificationUI;
        }

        private static void EnsureNotificationCardExists()
        {
            if (_notificationCard != null) return;

            var prefab = RequirePrefab(AlmediaLinkSettings.Load()?.NotificationCardPrefab, "Notification Card");
            if (prefab == null) return;

            var canvasGo = new GameObject("AlmediaLinkNotificationCanvas");
            Object.DontDestroyOnLoad(canvasGo);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(780, 1688);
            scaler.matchWidthOrHeight = 0.8f;

            canvasGo.AddComponent<GraphicRaycaster>();

            _notificationCard = Object.Instantiate(prefab, canvasGo.transform);
        }

        private static void EnsureActivityOverlayExists()
        {
            if (_activityOverlay != null) return;

            var prefab = RequirePrefab(AlmediaLinkSettings.Load()?.ActivityOverlayPrefab, "Activity Overlay");
            if (prefab == null) return;

            // Reuse the existing notification canvas
            if (_notificationCard == null)
            {
                AlmediaLog.Warning("Cannot create ActivityOverlay: NotificationCard does not exist.");
                return;
            }
            var canvas = _notificationCard.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                AlmediaLog.Warning("Cannot create ActivityOverlay: Notification Canvas not found.");
                return;
            }

            _activityOverlay = Object.Instantiate(prefab, canvas.transform);
        }

        // Warn-once resolver. The editor keeps these references in lockstep with the
        // EnableDefaultNotificationUI toggle, so a null here means a hand-edited settings asset.
        private static T RequirePrefab<T>(T prefab, string featureName) where T : Object
        {
            if (prefab != null) return prefab;
            if (_missingPrefabsLogged.Add(featureName))
            {
                AlmediaLog.Warning(
                    $"{featureName} prefab is not assigned in AlmediaLinkSettings while the default " +
                    "notification UI is enabled. Assign it in Almedia > Settings (re-ticking " +
                    "'Enable Default Notification UI' restores the bundled prefabs), or disable the default UI.");
            }
            return null;
        }
    }
}
