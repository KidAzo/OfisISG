using System;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    [Serializable]
    public class KitchenFireStateChangedEvent : UnityEvent<KitchenFireState> { }

    public sealed class KitchenFireStateController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private KitchenFireState currentState = KitchenFireState.None;

        [Header("Fire VFX Objects")]
        [SerializeField]
        private GameObject smallPanFire;

        [SerializeField]
        private GameObject growingPanFire;

        [SerializeField]
        private GameObject fireball;

        [SerializeField]
        private GameObject oilSpreadOnFloorFire;

        [SerializeField]
        private GameObject hoodFire;

        [SerializeField]
        private GameObject blanketSuppressionVfx;

        [SerializeField]
        private GameObject extinguisherSuppressionVfx;

        [SerializeField]
        private GameObject smokeVolume;

        [Header("Optional Audio")]
        [SerializeField]
        private AudioSource oilCrackleAudio;

        [SerializeField]
        private AudioSource growingFireAudio;

        [SerializeField]
        private AudioSource fireballAudio;

        [SerializeField]
        private AudioSource alarmFireAudio;

        [Header("Events")]
        [SerializeField]
        private KitchenFireStateChangedEvent onFireStateChanged = new KitchenFireStateChangedEvent();

        [SerializeField]
        private UnityEvent onSmallPanFire = new UnityEvent();

        [SerializeField]
        private UnityEvent onGrowingPanFire = new UnityEvent();

        [SerializeField]
        private UnityEvent onFireball = new UnityEvent();

        [SerializeField]
        private UnityEvent onOilSpreadOnFloor = new UnityEvent();

        [SerializeField]
        private UnityEvent onHoodSpread = new UnityEvent();

        [SerializeField]
        private UnityEvent onSuppressedByBlanket = new UnityEvent();

        [SerializeField]
        private UnityEvent onSuppressedByExtinguisher = new UnityEvent();

        [SerializeField]
        private UnityEvent onControlled = new UnityEvent();

        [SerializeField]
        private UnityEvent onUncontrolled = new UnityEvent();

        public KitchenFireState CurrentState => currentState;

        public KitchenFireStateChangedEvent OnFireStateChanged => onFireStateChanged;

        public void ChangeFireState(KitchenFireState nextState)
        {
            if (nextState == currentState)
            {
                return;
            }

            KitchenFireState previous = currentState;
            currentState = nextState;
            ApplyState(nextState);

            if (onFireStateChanged != null)
            {
                onFireStateChanged.Invoke(nextState);
            }

            Debug.Log(
                $"[KitchenFireStateController] Fire state changed: {previous} -> {nextState}",
                this);
        }

        public void ResetFire()
        {
            KitchenFireState previous = currentState;
            currentState = KitchenFireState.None;
            ApplyState(KitchenFireState.None);

            if (previous != KitchenFireState.None && onFireStateChanged != null)
            {
                onFireStateChanged.Invoke(KitchenFireState.None);
            }
        }

        public void StopAllFireAudio()
        {
            StopAudio(oilCrackleAudio);
            StopAudio(growingFireAudio);
            StopAudio(fireballAudio);
            StopAudio(alarmFireAudio);
        }

        private void ApplyState(KitchenFireState state)
        {
            DisableAllFireObjects();
            StopAllFireAudio();

            switch (state)
            {
                case KitchenFireState.None:
                    break;

                case KitchenFireState.SmallPanFire:
                    SetActive(smallPanFire, true);
                    SetActive(smokeVolume, true);
                    PlayAudio(oilCrackleAudio);
                    if (onSmallPanFire != null)
                    {
                        onSmallPanFire.Invoke();
                    }

                    break;

                case KitchenFireState.GrowingPanFire:
                    SetActive(growingPanFire, true);
                    SetActive(smokeVolume, true);
                    PlayAudio(growingFireAudio);
                    if (onGrowingPanFire != null)
                    {
                        onGrowingPanFire.Invoke();
                    }

                    break;

                case KitchenFireState.Fireball:
                    SetActive(fireball, true);
                    SetActive(growingPanFire, true);
                    SetActive(smokeVolume, true);
                    PlayAudio(fireballAudio);
                    if (onFireball != null)
                    {
                        onFireball.Invoke();
                    }

                    break;

                case KitchenFireState.OilSpreadOnFloor:
                    SetActive(oilSpreadOnFloorFire, true);
                    SetActive(growingPanFire, true);
                    SetActive(smokeVolume, true);
                    PlayAudio(growingFireAudio);
                    if (onOilSpreadOnFloor != null)
                    {
                        onOilSpreadOnFloor.Invoke();
                    }

                    break;

                case KitchenFireState.HoodSpread:
                    SetActive(hoodFire, true);
                    SetActive(growingPanFire, true);
                    SetActive(smokeVolume, true);
                    PlayAudio(growingFireAudio);
                    if (onHoodSpread != null)
                    {
                        onHoodSpread.Invoke();
                    }

                    break;

                case KitchenFireState.SuppressedByBlanket:
                    SetActive(blanketSuppressionVfx, true);
                    SetActive(smokeVolume, true);
                    if (onSuppressedByBlanket != null)
                    {
                        onSuppressedByBlanket.Invoke();
                    }

                    break;

                case KitchenFireState.SuppressedByExtinguisher:
                    SetActive(extinguisherSuppressionVfx, true);
                    SetActive(smokeVolume, true);
                    if (onSuppressedByExtinguisher != null)
                    {
                        onSuppressedByExtinguisher.Invoke();
                    }

                    break;

                case KitchenFireState.Controlled:
                    if (onControlled != null)
                    {
                        onControlled.Invoke();
                    }

                    break;

                case KitchenFireState.Uncontrolled:
                    SetActive(hoodFire, true);
                    SetActive(oilSpreadOnFloorFire, true);
                    SetActive(growingPanFire, true);
                    SetActive(smokeVolume, true);
                    if (alarmFireAudio != null)
                    {
                        PlayAudio(alarmFireAudio);
                    }
                    else
                    {
                        PlayAudio(growingFireAudio);
                    }

                    if (onUncontrolled != null)
                    {
                        onUncontrolled.Invoke();
                    }

                    break;
            }
        }

        private void DisableAllFireObjects()
        {
            SetActive(smallPanFire, false);
            SetActive(growingPanFire, false);
            SetActive(fireball, false);
            SetActive(oilSpreadOnFloorFire, false);
            SetActive(hoodFire, false);
            SetActive(blanketSuppressionVfx, false);
            SetActive(extinguisherSuppressionVfx, false);
            SetActive(smokeVolume, false);
        }

        private void SetActive(GameObject target, bool value)
        {
            if (target == null)
            {
                return;
            }

            if (target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }

        private void PlayAudio(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Play();
        }

        private void StopAudio(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
