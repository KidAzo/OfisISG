using UnityEngine;

namespace Woi.OfficeFire
{
    public readonly struct SelectionContext
    {
        public readonly SelectionSource Source;
        public readonly Transform Interactor;
        public readonly Ray Ray;
        public readonly RaycastHit Hit;

        public SelectionContext(SelectionSource source, Transform interactor, Ray ray, RaycastHit hit)
        {
            Source = source;
            Interactor = interactor;
            Ray = ray;
            Hit = hit;
        }
    }
}
