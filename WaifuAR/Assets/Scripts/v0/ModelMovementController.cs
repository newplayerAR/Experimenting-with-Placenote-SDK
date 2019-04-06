using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.iOS;
using UnityEngine.AI;

namespace ARKitPlaneSaver
{
    public class ModelMovementController : MonoBehaviour
    {
        public float distBeforeCam = 0.5f;
        private GameObject curModel;

        public GameObject CurModel { get => curModel; set => curModel = value; }

        public void OnComeHereClicked()
        {
            if (CurModel == null)
            {
                Debug.Log("Error: curModel is null.");
                return;
            }

            Vector3 camPos = Camera.main.transform.position + Camera.main.transform.forward * distBeforeCam;
            CurModel.transform.position = camPos;
        }

        public void OnComeHereClicked_NmA()
        {
            if (CurModel == null)
            {
                Debug.Log("Error: curModel is null.");
                return;
            }

            NavMeshAgent agent = CurModel.GetComponentInChildren<NavMeshAgent>();
            Vector3 camPos = Camera.main.transform.position + Camera.main.transform.forward * distBeforeCam;
            camPos.y = curModel.transform.position.y; // TODO:: will be changed to allow moving up/down through walking stairway / climbing

            agent.destination = camPos;
            agent.isStopped = false;
        }
    }
}