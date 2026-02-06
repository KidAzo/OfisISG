using System;
using UnityEngine;
using UnityEngine.Events;


namespace HazardSystem.NPC
{
    public class PathWalker : MonoBehaviour
    {
        [SerializeField] Transform[] npcs;
        [SerializeField] Transform[] waypoints;
        [SerializeField] float speed = 2f;
        [SerializeField] float arriveDistance = 0.1f;

        [SerializeField] bool loop = false;
        [SerializeField] float rotationSpeed = 12f;

        [SerializeField] UnityEvent onReachedFirstWaypoint;
        [SerializeField] UnityEvent onReachedLastWaypoint;
        [SerializeField] private bool canEventsInvoke;

        int _index;

        void Update()
        {
            Transform target = waypoints[_index];
            Vector3 pos = transform.position;
            Vector3 dest = target.position;

            foreach (var npc in npcs)
            {
                npc.position = Vector3.MoveTowards(npc.position, dest, speed * Time.deltaTime);
            }

            Vector3 dir = (dest - pos);
            dir.y = 0;

            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);

            if ((transform.position - dest).sqrMagnitude <= arriveDistance * arriveDistance)
            {
                if (loop)
                {
                    if (_index == 0 && canEventsInvoke)
                        onReachedFirstWaypoint?.Invoke();
                    else if (_index == waypoints.Length - 1 && canEventsInvoke)
                        onReachedLastWaypoint?.Invoke();

                    _index = (_index + 1) % waypoints.Length;
                }
                else
                {
                    _index = Mathf.Min(_index + 1, waypoints.Length - 1);
                }
            }
        }

        public void SetSpeedZero()
        {
            speed = 0f;
        }

        public void SetCanInvokeEvents(bool canInvoke)
        {
            canEventsInvoke = canInvoke;
        }

        public void SetAnimationUpToIndex()
        {
            int index = _index;
            if (index == 0)
            {
                onReachedLastWaypoint?.Invoke();
            }
            else if (index == waypoints.Length - 1)
            {
                onReachedFirstWaypoint?.Invoke();
            }
        }
    }
}


