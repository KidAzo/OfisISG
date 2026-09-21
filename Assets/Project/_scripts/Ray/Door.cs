using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Tweenables;

public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] float openedAngle;
    float firstYAngle;
    bool isOpened;
    Tween tween;

    void Start()
    {
        firstYAngle = transform.eulerAngles.y;
    }

    public void Interact()
    {
        Action action = isOpened ? Close : Open;
        action.Invoke();
    }

    void Open()
    {
        tween.Stop();

        isOpened = true;
        tween = Tween.LocalRotation(
            transform,
            new Vector3(0, openedAngle, 0),
            1f,
            Ease.InOutSine
        );
    }

    void Close()
    {
        tween.Stop();

        isOpened = false;
        tween = Tween.LocalRotation(
            transform,
            new Vector3(0, firstYAngle, 0),
            1f,
            Ease.InOutSine
        );
    }

}