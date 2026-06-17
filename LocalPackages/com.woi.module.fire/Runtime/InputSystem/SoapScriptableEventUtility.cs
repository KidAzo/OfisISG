using System;
using System.Collections.Generic;
using System.Reflection;
using Obvious.Soap;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Woi.InputSystem
{
    /// <summary>
    /// Guards Soap <see cref="ScriptableEventNoParam"/> raises against destroyed Unity listener targets
    /// (common after additive scene unload when E still fires Interact and Equip).
    /// </summary>
    public static class SoapScriptableEventUtility
    {
        private static readonly FieldInfo OnRaisedField = typeof(ScriptableEventNoParam).GetField(
            "_onRaised",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo EventListenersField = typeof(ScriptableEventNoParam).GetField(
            "_eventListeners",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo DebugLogField = typeof(ScriptableEventBase).GetField(
            "_debugLogEnabled",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo OnEventRaisedMethod = typeof(EventListenerNoParam).GetMethod(
            "OnEventRaised",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RaiseNoParam(ScriptableEventNoParam evt)
        {
            if (evt == null || !Application.isPlaying)
            {
                return;
            }

            PruneDestroyedListeners(evt);
            RaiseEventListeners(evt);
            RaiseCodeListeners(evt);
        }

        public static void PruneDestroyedListeners(ScriptableEventNoParam evt)
        {
            if (evt == null)
            {
                return;
            }

            PruneDestroyedEventListeners(evt);
            PruneDestroyedCodeListeners(evt);
        }

        public static void PruneGameplayNoParamListeners(GameplayInputContext gameplay)
        {
            if (gameplay == null)
            {
                return;
            }

            PruneDestroyedListeners(gameplay.InteractEvent);
            PruneDestroyedListeners(gameplay.EquipEvent);
            PruneDestroyedListeners(gameplay.DropEvent);
            PruneDestroyedListeners(gameplay.PinPulling);
        }

        private static void RaiseEventListeners(ScriptableEventNoParam evt)
        {
            if (EventListenersField?.GetValue(evt) is not List<EventListenerNoParam> listeners)
            {
                return;
            }

            if (OnEventRaisedMethod == null)
            {
                Debug.LogError("[SoapScriptableEventUtility] Could not resolve EventListenerNoParam.OnEventRaised.");
                return;
            }

            bool debug = DebugLogField?.GetValue(evt) is bool debugEnabled && debugEnabled;

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                EventListenerNoParam listener = listeners[i];
                if (listener == null)
                {
                    listeners.RemoveAt(i);
                    continue;
                }

                try
                {
                    OnEventRaisedMethod.Invoke(listener, new object[] { evt, debug });
                }
                catch (MissingReferenceException ex)
                {
                    listeners.RemoveAt(i);
                    LogPruned(evt, ex.Message);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is MissingReferenceException inner)
                {
                    listeners.RemoveAt(i);
                    LogPruned(evt, inner.Message);
                }
            }
        }

        private static void RaiseCodeListeners(ScriptableEventNoParam evt)
        {
            if (OnRaisedField?.GetValue(evt) is not Action onRaised || onRaised == null)
            {
                return;
            }

            Delegate[] invocationList = onRaised.GetInvocationList();
            if (invocationList == null || invocationList.Length == 0)
            {
                return;
            }

            Action cleaned = null;

            for (int i = 0; i < invocationList.Length; i++)
            {
                if (invocationList[i] is not Action action)
                {
                    continue;
                }

                if (invocationList[i].Target is Object unityTarget && unityTarget == null)
                {
                    continue;
                }

                try
                {
                    action.Invoke();
                    cleaned += action;
                }
                catch (MissingReferenceException ex)
                {
                    LogPruned(evt, ex.Message);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is MissingReferenceException inner)
                {
                    LogPruned(evt, inner.Message);
                }
            }

            OnRaisedField.SetValue(evt, cleaned);
        }

        private static void PruneDestroyedEventListeners(ScriptableEventNoParam evt)
        {
            if (EventListenersField?.GetValue(evt) is not List<EventListenerNoParam> listeners)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                EventListenerNoParam listener = listeners[i];
                if (listener == null)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private static void PruneDestroyedCodeListeners(ScriptableEventNoParam evt)
        {
            if (OnRaisedField == null)
            {
                return;
            }

            if (OnRaisedField.GetValue(evt) is not Action onRaised || onRaised == null)
            {
                return;
            }

            Delegate[] invocationList = onRaised.GetInvocationList();
            if (invocationList == null || invocationList.Length == 0)
            {
                return;
            }

            Action cleaned = null;
            bool removedAny = false;

            for (int i = 0; i < invocationList.Length; i++)
            {
                Delegate del = invocationList[i];
                if (del is not Action action)
                {
                    continue;
                }

                if (del.Target is Object unityTarget && unityTarget == null)
                {
                    removedAny = true;
                    continue;
                }

                cleaned += action;
            }

            if (!removedAny)
            {
                return;
            }

            OnRaisedField.SetValue(evt, cleaned);
        }

        private static void LogPruned(ScriptableEventNoParam evt, string message)
        {
            Debug.LogWarning(
                $"[SoapScriptableEventUtility] Pruned stale listener on '{evt.name}': {message}");
        }
    }
}
