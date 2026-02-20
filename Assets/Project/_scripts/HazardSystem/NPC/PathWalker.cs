using System;
using NUnit.Framework;
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
        [SerializeField] UnityEvent collisionEvent;
        [SerializeField] UnityEvent reachedEvent;
        [SerializeField] private bool canEventsInvoke;
        [SerializeField] private bool canReset;
        [SerializeField] private bool canIncrease;
        bool isWorked;
        bool isReached;
        int _index;
        float defaultSpeed;

        void Start()
        {
            defaultSpeed = speed;
        }

        void Update()
        {
            Transform target = waypoints[_index];
            Vector3 pos = transform.position;
            Vector3 dest = target.position;

            foreach (var npc in npcs)
            {
                if (isWorked) return;
                npc.position = Vector3.MoveTowards(npc.position, dest, speed * Time.deltaTime);
            }

            Vector3 dir = (dest - pos);
            dir.y = 0;

            if (dir.sqrMagnitude > 0.0001f)
            {
                foreach (var npc in npcs)
                    npc.transform.rotation = Quaternion.Slerp(npc.transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            }

            if ((npcs[0].position - dest).sqrMagnitude <= arriveDistance * arriveDistance)
            {
                if (loop)
                {
                    if (_index == 0 && canEventsInvoke)
                    {
                        onReachedFirstWaypoint?.Invoke();

                        if (canReset)
                        {
                            isWorked = true;
                            Invoke(nameof(ResetIndex), 2f);
                        }

                        if(canIncrease)
                             _index = (_index + 1) % waypoints.Length;
                        return;
                    }
                    else if (_index == waypoints.Length - 1 && canEventsInvoke)
                    {
                        onReachedLastWaypoint?.Invoke();
                    }

                    _index = (_index + 1) % waypoints.Length;
                }
                else
                {
                    if (_index == waypoints.Length - 1)
                    {
                        if (!isWorked)
                        {
                            collisionEvent?.Invoke();
                            isWorked = true;
                        }
                        
                        return;
                    }
                    _index = Mathf.Min(_index + 1, waypoints.Length - 1);
                }
            }
        }

        void ResetIndex()
        {
            _index = 0;
            isWorked = false;
        }

        public void SetSpeedZero()
        {
            speed = 0f;
        }


        public void SetSpeedDefault()
        {
            speed = defaultSpeed;
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


