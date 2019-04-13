using UnityEngine;
using System.Collections;
using UnityEngine.AI;

namespace UnityChan
{
    // Require these components when using this script
    [RequireComponent(typeof(Animator))]

    public class MovingAnimationController : MonoBehaviour
    {
        Animator animator;
        NavMeshAgent agent;

        void Awake()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
        }

        // Update is called once per frame
        void Update()
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }
    }
}
