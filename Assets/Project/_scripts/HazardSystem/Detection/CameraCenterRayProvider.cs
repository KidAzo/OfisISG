using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IRayProvider
{
    Ray GetRay();
}

public sealed class ScreenCenterRayProvider : IRayProvider
{
    readonly Camera _camera;

    public ScreenCenterRayProvider(Camera camera)
    {
        _camera = camera;
    }

    public Ray GetRay()
    {
        // merkezden ray
        var center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        return _camera.ScreenPointToRay(center);
    }
}

public interface IRaycastService
{
    bool TryRaycast(Ray ray, float maxDistance, LayerMask mask, out RaycastHit hit);
}

public sealed class PhysicsRaycastService : IRaycastService
{
    public bool TryRaycast(Ray ray, float maxDistance, LayerMask mask, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, maxDistance, mask, QueryTriggerInteraction.Ignore);
    }
}

public interface IHitSelector<T> where T : IInteractable
{
    bool TrySelect(in RaycastHit hit, out T result);
}

public sealed class RaySelector : IHitSelector<IInteractable>
{
    public bool TrySelect(in RaycastHit hit, out IInteractable result)
    {
        result = null;

        var go = hit.collider ? hit.collider.gameObject : null;
        if (go == null) return false;

        if (!go.TryGetComponent(out IInteractable interactable)) return false;

        result = interactable;
        return true;
    }
}

public sealed class RayInteractor<T> where T : IInteractable
{
    readonly IRayProvider _rayProvider;
    readonly IRaycastService _raycast;
    readonly IHitSelector<T> _selector;

    public RayInteractor(IRayProvider rayProvider, IRaycastService raycast, IHitSelector<T> selector)
    {
        _rayProvider = rayProvider;
        _raycast = raycast;
        _selector = selector;
    }

    public bool TryGetTarget(float maxDistance, LayerMask mask, out RaycastHit hit, out T target)
    {
        target = default;
        hit = default;

        var ray = _rayProvider.GetRay();
        if (!_raycast.TryRaycast(ray, maxDistance, mask, out hit)) return false;

        return _selector.TrySelect(hit, out target);
    }
}

public interface IInteractable
{
	void Interact();
}



