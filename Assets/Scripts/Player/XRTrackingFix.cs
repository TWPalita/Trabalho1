using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Input System TrackedPoseDriver with referenced XRI actions can latch tracking state to
/// None on the first update if the HMD/bindings are not ready yet. Device-change callbacks
/// do not re-read tracking state when a tracking-state action exists, so look stays frozen
/// until Pause/Unpause (OnEnable → fresh first update). This component keeps action assets
/// enabled, waits for pose readiness, then pulse-restarts every TrackedPoseDriver.
/// </summary>
public sealed class XRTrackingFix : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Max seconds to wait for HMD / pose bindings before forcing a TrackedPoseDriver restart.")]
    float m_MaxWaitSeconds = 8f;

    [SerializeField]
    [Tooltip("Seconds to keep TrackedPoseDriver disabled during a restart pulse.")]
    float m_DisablePulseSeconds = 0.05f;

    [SerializeField]
    [Tooltip("Retry interval while waiting for pose readiness.")]
    float m_PollIntervalSeconds = 0.1f;

    Coroutine m_RestartRoutine;
    bool m_Subscribed;

    void OnEnable()
    {
        EnsureInputActionsEnabled();

        if (!m_Subscribed)
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            Application.focusChanged += OnFocusChanged;
            m_Subscribed = true;
        }

        RequestRestart("OnEnable");
    }

    void OnDisable()
    {
        if (m_RestartRoutine != null)
        {
            StopCoroutine(m_RestartRoutine);
            m_RestartRoutine = null;
        }

        if (m_Subscribed)
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            Application.focusChanged -= OnFocusChanged;
            m_Subscribed = false;
        }
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added &&
            change != InputDeviceChange.Reconnected &&
            change != InputDeviceChange.Enabled &&
            change != InputDeviceChange.ConfigurationChanged)
        {
            return;
        }

        if (device is XRHMD || device is XRController)
            RequestRestart($"Device:{change}:{device.displayName}");
    }

    void OnFocusChanged(bool hasFocus)
    {
        if (hasFocus)
            RequestRestart("FocusGained");
    }

    void RequestRestart(string reason)
    {
        if (!isActiveAndEnabled)
            return;

        if (m_RestartRoutine != null)
            StopCoroutine(m_RestartRoutine);

        m_RestartRoutine = StartCoroutine(RestartWhenReady(reason));
    }

    IEnumerator RestartWhenReady(string reason)
    {
        EnsureInputActionsEnabled();

        float elapsed = 0f;
        while (elapsed < m_MaxWaitSeconds && !IsPoseReady())
        {
            yield return new WaitForSecondsRealtime(m_PollIntervalSeconds);
            elapsed += m_PollIntervalSeconds;
            EnsureInputActionsEnabled();
        }

        // One extra frame so Input System can settle after enable/device add.
        yield return null;

        TrackedPoseDriver[] drivers =
            FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (drivers == null || drivers.Length == 0)
        {
            Debug.LogWarning($"[XRTrackingFix] Nenhum TrackedPoseDriver encontrado ({reason}).", this);
            m_RestartRoutine = null;
            yield break;
        }

        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] != null)
                drivers[i].enabled = false;
        }

        yield return new WaitForSecondsRealtime(m_DisablePulseSeconds);
        yield return null;

        EnsureInputActionsEnabled();

        int reenabled = 0;
        for (int i = 0; i < drivers.Length; i++)
        {
            TrackedPoseDriver driver = drivers[i];
            if (driver == null)
                continue;

            driver.enabled = true;
            reenabled++;
        }

        Debug.Log(
            $"[XRTrackingFix] TrackedPoseDriver restart OK ({reenabled}, reason={reason}, waited={elapsed:0.00}s, ready={IsPoseReady()}).",
            this);

        m_RestartRoutine = null;
    }

    void EnsureInputActionsEnabled()
    {
        InputActionManager[] managers =
            FindObjectsByType<InputActionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < managers.Length; i++)
        {
            InputActionManager manager = managers[i];
            if (manager == null)
                continue;

            manager.EnableInput();
        }
    }

    static bool IsPoseReady()
    {
        if (InputSystem.GetDevice<XRHMD>() != null)
            return true;

        TrackedPoseDriver[] drivers =
            FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < drivers.Length; i++)
        {
            TrackedPoseDriver driver = drivers[i];
            if (driver == null)
                continue;

            if (HasEnabledResolvedPose(driver.positionInput.action) ||
                HasEnabledResolvedPose(driver.rotationInput.action))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasEnabledResolvedPose(InputAction action)
    {
        if (action == null || !action.enabled)
            return false;

        try
        {
            return action.controls.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
