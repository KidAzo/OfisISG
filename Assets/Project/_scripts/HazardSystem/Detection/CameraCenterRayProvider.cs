using System.Linq;
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

public interface IHitSelector<T> where T : IRayTarget
{
    bool TrySelect(in RaycastHit hit, out T result);
    bool TrySelectNearest(Ray ray, float maxDistance, LayerMask mask, out T result);
}

public sealed class RaySelector : IHitSelector<IRayTarget>
{
    public bool TrySelect(in RaycastHit hit, out IRayTarget result)
    {
        result = null;

        var go = hit.collider ? hit.collider.gameObject : null;
        if (go == null) return false;

        if (!go.TryGetComponent(out IRayTarget interactable)) return false;

        result = interactable;
        return true;
    }

    public bool TrySelectNearest(Ray ray, float maxDistance, LayerMask mask, out IRayTarget result)
    {
        result = null;

        var hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        var rayOrigin = ray.origin;
        IRayTarget nearest = null;
        float minDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            var go = hit.collider ? hit.collider.gameObject : null;
            if (go == null) continue;

            var interactables = go.GetComponents<IRayTarget>();
            foreach (var interactable in interactables)
            {
                if (interactable == null) continue;
                if (interactable is Component c)
                {
                    float distance = Vector3.Distance(rayOrigin, c.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = interactable;
                    }
                }
            }
        }

        result = nearest;
        return result != null;
    }
}

public sealed class RayInteractor<T> where T : IRayTarget
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

    public bool TryGetTarget(float maxDistance, LayerMask mask, out T target)
    {
        target = default;
        var ray = _rayProvider.GetRay();
        return _selector.TrySelectNearest(ray, maxDistance, mask, out target);
    }
}

public interface IInteractable : IRayTarget
{
	void Interact();
}

public interface IRayTarget

{  
}



